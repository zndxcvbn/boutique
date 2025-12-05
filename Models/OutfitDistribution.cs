using Mutagen.Bethesda.Plugins;

namespace Boutique.Models;

/// <summary>
/// Represents a single outfit distribution from a SPID or SkyPatcher file.
/// </summary>
public sealed record OutfitDistribution(
    /// <summary>The source distribution file path</summary>
    string FilePath,
    /// <summary>The filename for display</summary>
    string FileName,
    /// <summary>SPID or SkyPatcher</summary>
    DistributionFileType FileType,
    /// <summary>The outfit FormKey being assigned</summary>
    FormKey OutfitFormKey,
    /// <summary>The outfit EditorID (if resolvable)</summary>
    string? OutfitEditorId,
    /// <summary>Processing order index (lower = processed earlier, higher = wins)</summary>
    int ProcessingOrder,
    /// <summary>Whether this distribution is the winner (last in processing order)</summary>
    bool IsWinner);
