using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Boutique.Models;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Skyrim;
using Serilog;

namespace Boutique.Services;

public class NpcOutfitResolutionService : INpcOutfitResolutionService
{
    private readonly IMutagenService _mutagenService;
    private readonly ILogger _logger;

    public NpcOutfitResolutionService(IMutagenService mutagenService, ILogger logger)
    {
        _mutagenService = mutagenService;
        _logger = logger.ForContext<NpcOutfitResolutionService>();
    }

    public async Task<IReadOnlyList<NpcOutfitAssignment>> ResolveNpcOutfitsAsync(
        IReadOnlyList<DistributionFile> distributionFiles,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run<IReadOnlyList<NpcOutfitAssignment>>(() =>
        {
            if (_mutagenService.LinkCache is not ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache)
            {
                _logger.Warning("LinkCache not available for NPC outfit resolution.");
                return Array.Empty<NpcOutfitAssignment>();
            }

            try
            {
                // Sort files: SPID files alphabetically, then SkyPatcher files alphabetically
                var sortedFiles = SortDistributionFiles(distributionFiles);
                _logger.Information("Processing {Count} distribution files in order", sortedFiles.Count);

                // Build a dictionary of NPC FormKey -> list of distributions
                var npcDistributions = new Dictionary<FormKey, List<OutfitDistribution>>();

                // Cache all NPCs for NPC identifier resolution
                var allNpcs = linkCache.WinningOverrides<INpcGetter>().ToList();
                var npcByEditorId = allNpcs
                    .Where(n => !string.IsNullOrWhiteSpace(n.EditorID))
                    .ToDictionary(n => n.EditorID!, n => n, StringComparer.OrdinalIgnoreCase);
                var npcByName = allNpcs
                    .Where(n => !string.IsNullOrWhiteSpace(n.Name?.String))
                    .GroupBy(n => n.Name!.String!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                // Process each file in order
                for (int fileIndex = 0; fileIndex < sortedFiles.Count; fileIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var file = sortedFiles[fileIndex];
                    
                    ProcessDistributionFile(file, fileIndex, linkCache, npcByEditorId, npcByName, npcDistributions);
                }

                // Build the final NPC outfit assignments
                var assignments = BuildNpcOutfitAssignments(npcDistributions, linkCache, allNpcs);
                
                _logger.Information("Resolved outfit assignments for {Count} NPCs", assignments.Count);
                return assignments;
            }
            catch (OperationCanceledException)
            {
                _logger.Information("NPC outfit resolution cancelled.");
                return Array.Empty<NpcOutfitAssignment>();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to resolve NPC outfit assignments.");
                return Array.Empty<NpcOutfitAssignment>();
            }
        }, cancellationToken);
    }

    private List<DistributionFile> SortDistributionFiles(IReadOnlyList<DistributionFile> files)
    {
        // SPID files first (alphabetically by filename), then SkyPatcher files (alphabetically by filename)
        var spidFiles = files
            .Where(f => f.Type == DistributionFileType.Spid)
            .OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var skyPatcherFiles = files
            .Where(f => f.Type == DistributionFileType.SkyPatcher)
            .OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sorted = new List<DistributionFile>();
        sorted.AddRange(spidFiles);
        sorted.AddRange(skyPatcherFiles);

        foreach (var file in sorted)
        {
            _logger.Debug("Processing order: {Type} - {FileName}", file.Type, file.FileName);
        }

        return sorted;
    }

    private void ProcessDistributionFile(
        DistributionFile file,
        int processingOrder,
        ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache,
        Dictionary<string, INpcGetter> npcByEditorId,
        Dictionary<string, INpcGetter> npcByName,
        Dictionary<FormKey, List<OutfitDistribution>> npcDistributions)
    {
        foreach (var line in file.Lines)
        {
            if (!line.IsOutfitDistribution)
                continue;

            // Parse the line to extract NPC targets and outfit
            var parsedEntries = ParseDistributionLine(file, line, linkCache, npcByEditorId, npcByName);

            foreach (var (npcFormKey, outfitFormKey, outfitEditorId) in parsedEntries)
            {
                if (!npcDistributions.TryGetValue(npcFormKey, out var distributions))
                {
                    distributions = new List<OutfitDistribution>();
                    npcDistributions[npcFormKey] = distributions;
                }

                distributions.Add(new OutfitDistribution(
                    FilePath: file.FullPath,
                    FileName: file.FileName,
                    FileType: file.Type,
                    OutfitFormKey: outfitFormKey,
                    OutfitEditorId: outfitEditorId,
                    ProcessingOrder: processingOrder,
                    IsWinner: false // Will be set later
                ));
            }
        }
    }

    private List<(FormKey NpcFormKey, FormKey OutfitFormKey, string? OutfitEditorId)> ParseDistributionLine(
        DistributionFile file,
        DistributionLine line,
        ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache,
        Dictionary<string, INpcGetter> npcByEditorId,
        Dictionary<string, INpcGetter> npcByName)
    {
        var results = new List<(FormKey, FormKey, string?)>();

        if (file.Type == DistributionFileType.SkyPatcher)
        {
            ParseSkyPatcherLine(line.RawText, linkCache, npcByEditorId, npcByName, results);
        }
        else if (file.Type == DistributionFileType.Spid)
        {
            ParseSpidLine(line.RawText, linkCache, npcByEditorId, npcByName, results);
        }

        return results;
    }

    private void ParseSkyPatcherLine(
        string lineText,
        ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache,
        Dictionary<string, INpcGetter> npcByEditorId,
        Dictionary<string, INpcGetter> npcByName,
        List<(FormKey, FormKey, string?)> results)
    {
        // SkyPatcher format: filterByNpcs=ModKey|FormID,ModKey|FormID:outfitDefault=ModKey|FormID
        var trimmed = lineText.Trim();
        
        // Extract NPC FormKeys
        var npcFormKeys = new List<FormKey>();
        var filterByNpcsIndex = trimmed.IndexOf("filterByNpcs=", StringComparison.OrdinalIgnoreCase);
        if (filterByNpcsIndex >= 0)
        {
            var npcStart = filterByNpcsIndex + "filterByNpcs=".Length;
            var npcEnd = trimmed.IndexOf(':', npcStart);
            if (npcEnd > npcStart)
            {
                var npcString = trimmed.Substring(npcStart, npcEnd - npcStart);
                foreach (var npcPart in npcString.Split(','))
                {
                    var formKey = TryParseFormKey(npcPart.Trim());
                    if (formKey.HasValue)
                    {
                        npcFormKeys.Add(formKey.Value);
                    }
                }
            }
        }

        // Extract outfit FormKey
        FormKey? outfitFormKey = null;
        string? outfitEditorId = null;
        var outfitDefaultIndex = trimmed.IndexOf("outfitDefault=", StringComparison.OrdinalIgnoreCase);
        if (outfitDefaultIndex >= 0)
        {
            var outfitStart = outfitDefaultIndex + "outfitDefault=".Length;
            var outfitEnd = trimmed.IndexOf(':', outfitStart);
            var outfitString = outfitEnd > outfitStart 
                ? trimmed.Substring(outfitStart, outfitEnd - outfitStart) 
                : trimmed.Substring(outfitStart);
            
            outfitFormKey = TryParseFormKey(outfitString.Trim());
            if (outfitFormKey.HasValue && linkCache.TryResolve<IOutfitGetter>(outfitFormKey.Value, out var outfit))
            {
                outfitEditorId = outfit.EditorID;
            }
        }

        // Also check filterByOutfits= syntax (alternative SkyPatcher format)
        if (!outfitFormKey.HasValue)
        {
            var filterByOutfitsIndex = trimmed.IndexOf("filterByOutfits=", StringComparison.OrdinalIgnoreCase);
            if (filterByOutfitsIndex >= 0)
            {
                var outfitStart = filterByOutfitsIndex + "filterByOutfits=".Length;
                var outfitEnd = trimmed.IndexOf(':', outfitStart);
                var outfitString = outfitEnd > outfitStart 
                    ? trimmed.Substring(outfitStart, outfitEnd - outfitStart) 
                    : trimmed.Substring(outfitStart);
                
                outfitFormKey = TryParseFormKey(outfitString.Trim());
                if (outfitFormKey.HasValue && linkCache.TryResolve<IOutfitGetter>(outfitFormKey.Value, out var outfit))
                {
                    outfitEditorId = outfit.EditorID;
                }
            }
        }

        // Create results for each NPC-outfit combination
        if (outfitFormKey.HasValue && npcFormKeys.Count > 0)
        {
            foreach (var npcFormKey in npcFormKeys)
            {
                results.Add((npcFormKey, outfitFormKey.Value, outfitEditorId));
            }
        }
    }

    private void ParseSpidLine(
        string lineText,
        ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache,
        Dictionary<string, INpcGetter> npcByEditorId,
        Dictionary<string, INpcGetter> npcByName,
        List<(FormKey, FormKey, string?)> results)
    {
        // SPID format: Outfit = 0x800~ModKey|EditorID[,EditorID,...]
        var trimmed = lineText.Trim();
        
        if (!trimmed.StartsWith("Outfit", StringComparison.OrdinalIgnoreCase))
            return;

        var equalsIndex = trimmed.IndexOf('=');
        if (equalsIndex < 0)
            return;

        var valuePart = trimmed.Substring(equalsIndex + 1).Trim();
        
        // Find the ~ separator between FormID and ModKey
        var tildeIndex = valuePart.IndexOf('~');
        if (tildeIndex < 0)
            return;

        var formIdString = valuePart.Substring(0, tildeIndex).Trim();
        var rest = valuePart.Substring(tildeIndex + 1).Trim();

        // Parse FormID
        formIdString = formIdString.Replace("0x", "").Replace("0X", "");
        if (!uint.TryParse(formIdString, System.Globalization.NumberStyles.HexNumber, null, out var formId))
            return;

        // Find the | separator between ModKey and EditorIDs
        var pipeIndex = rest.IndexOf('|');
        if (pipeIndex < 0)
            return;

        var modKeyString = rest.Substring(0, pipeIndex).Trim();
        var editorIdsString = rest.Substring(pipeIndex + 1).Trim();

        if (!ModKey.TryFromNameAndExtension(modKeyString, out var modKey))
            return;

        // Create FormKey for the outfit
        var outfitFormKey = new FormKey(modKey, formId);
        string? outfitEditorId = null;
        
        if (linkCache.TryResolve<IOutfitGetter>(outfitFormKey, out var outfit))
        {
            outfitEditorId = outfit.EditorID;
        }

        // Parse NPC identifiers (comma-separated)
        var npcIdentifiers = editorIdsString
            .Split(',')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        foreach (var identifier in npcIdentifiers)
        {
            // Try to find NPC by EditorID first, then by Name
            INpcGetter? npc = null;
            if (npcByEditorId.TryGetValue(identifier, out var npcById))
            {
                npc = npcById;
            }
            else if (npcByName.TryGetValue(identifier, out var npcByNameMatch))
            {
                npc = npcByNameMatch;
            }

            if (npc != null)
            {
                results.Add((npc.FormKey, outfitFormKey, outfitEditorId));
            }
        }
    }

    private List<NpcOutfitAssignment> BuildNpcOutfitAssignments(
        Dictionary<FormKey, List<OutfitDistribution>> npcDistributions,
        ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache,
        List<INpcGetter> allNpcs)
    {
        var assignments = new List<NpcOutfitAssignment>();
        var npcLookup = allNpcs.ToDictionary(n => n.FormKey);

        foreach (var (npcFormKey, distributions) in npcDistributions)
        {
            // Sort distributions by processing order
            var sortedDistributions = distributions
                .OrderBy(d => d.ProcessingOrder)
                .ToList();

            // Mark the winner (last one)
            var winnerIndex = sortedDistributions.Count - 1;
            var updatedDistributions = sortedDistributions
                .Select((d, i) => d with { IsWinner = i == winnerIndex })
                .ToList();

            // Get the winning distribution
            var winner = updatedDistributions[winnerIndex];

            // Get NPC info
            string? editorId = null;
            string? name = null;
            ModKey sourceMod = npcFormKey.ModKey;

            if (npcLookup.TryGetValue(npcFormKey, out var npc))
            {
                editorId = npc.EditorID;
                name = npc.Name?.String;
                
                // Find original master
                try
                {
                    var contexts = linkCache.ResolveAllContexts<INpc, INpcGetter>(npcFormKey);
                    var firstContext = contexts.FirstOrDefault();
                    if (firstContext != null)
                    {
                        sourceMod = firstContext.ModKey;
                    }
                }
                catch
                {
                    // Fallback to FormKey.ModKey
                }
            }

            assignments.Add(new NpcOutfitAssignment(
                NpcFormKey: npcFormKey,
                EditorId: editorId,
                Name: name,
                SourceMod: sourceMod,
                FinalOutfitFormKey: winner.OutfitFormKey,
                FinalOutfitEditorId: winner.OutfitEditorId,
                Distributions: updatedDistributions,
                HasConflict: updatedDistributions.Count > 1
            ));
        }

        // Sort by display name for UI
        return assignments
            .OrderBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static FormKey? TryParseFormKey(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var trimmed = text.Trim();
        var pipeIndex = trimmed.IndexOf('|');
        if (pipeIndex < 0)
            return null;

        var modKeyString = trimmed.Substring(0, pipeIndex).Trim();
        var formIdString = trimmed.Substring(pipeIndex + 1).Trim();

        if (!ModKey.TryFromNameAndExtension(modKeyString, out var modKey))
            return null;

        // Handle hex format with or without 0x prefix
        formIdString = formIdString.Replace("0x", "").Replace("0X", "");
        if (!uint.TryParse(formIdString, System.Globalization.NumberStyles.HexNumber, null, out var formId))
            return null;

        return new FormKey(modKey, formId);
    }
}
