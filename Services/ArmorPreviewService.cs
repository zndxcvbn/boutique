using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using NiflySharp;
using NiflySharp.Blocks;
using RequiemGlamPatcher.Models;
using RequiemGlamPatcher.ViewModels;
using Serilog;

namespace RequiemGlamPatcher.Services;

public class ArmorPreviewService : IArmorPreviewService
{
    private readonly IMutagenService _mutagenService;
    private readonly ILogger _logger;

    private static readonly string FemaleBodyRelativePath = Path.Combine("meshes", "actors", "character", "character assets", "femalebody_0.nif");
    private static readonly string MaleBodyRelativePath = Path.Combine("meshes", "actors", "character", "character assets", "malebody_0.nif");

    public ArmorPreviewService(IMutagenService mutagenService, ILogger logger)
    {
        _mutagenService = mutagenService;
        _logger = logger.ForContext<ArmorPreviewService>();
    }

    public async Task<ArmorPreviewScene> BuildPreviewAsync(
        IEnumerable<ArmorRecordViewModel> armorPieces,
        GenderedModelVariant preferredGender,
        CancellationToken cancellationToken = default)
    {
        if (!_mutagenService.IsInitialized)
            throw new InvalidOperationException("Mutagen service has not been initialized.");

        var dataPath = _mutagenService.DataFolderPath;
        var linkCache = _mutagenService.LinkCache;

        if (string.IsNullOrWhiteSpace(dataPath) || !Directory.Exists(dataPath))
            throw new DirectoryNotFoundException("Skyrim Data path is not set or does not exist.");

        if (linkCache == null)
            throw new InvalidOperationException("Link cache is not available.");

        var pieces = armorPieces?.ToList() ?? new List<ArmorRecordViewModel>();
        return await Task.Run(() => BuildPreviewInternal(pieces, preferredGender, dataPath, linkCache, cancellationToken), cancellationToken);
    }

    private ArmorPreviewScene BuildPreviewInternal(
        IReadOnlyList<ArmorRecordViewModel> pieces,
        GenderedModelVariant preferredGender,
        string dataPath,
        ILinkCache linkCache,
        CancellationToken cancellationToken)
    {
        var gender = DetermineEffectiveGender(pieces, preferredGender, linkCache);
        var meshes = new List<PreviewMeshShape>();
        var missingAssets = new List<string>();

        // Always add baseline body mesh
        var bodyPath = GetBodyPath(dataPath, gender);
        if (File.Exists(bodyPath))
        {
            meshes.AddRange(LoadMeshesFromNif("Base Body", bodyPath, dataPath, gender, cancellationToken));
        }
        else
        {
            missingAssets.Add(bodyPath);
            _logger.Warning("Base body mesh not found at {BodyPath}", bodyPath);
        }

        var visitedParts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var piece in pieces)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var armor = piece.Armor;
            var armorName = armor.Name?.String ?? armor.EditorID ?? "Unknown Armor";

            foreach (var addonLink in armor.Armature)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!linkCache.TryResolve<IArmorAddonGetter>(addonLink.FormKey, out var addon) || addon is null)
                {
                    _logger.Warning("Failed to resolve ArmorAddon {FormKey} for armor {Armor}", addonLink.FormKey, armorName);
                    continue;
                }

                var model = SelectModel(addon.WorldModel, gender, out var variantForAddon);
                if (model == null)
                {
                    _logger.Information("ArmorAddon {Addon} has no usable models for gender {Gender}", addon.EditorID, gender);
                    continue;
                }

                var modelPath = ResolveModelPath(model);
                if (string.IsNullOrWhiteSpace(modelPath))
                {
                    _logger.Information("ArmorAddon {Addon} model is missing a file path.", addon.EditorID);
                    continue;
                }

                var fullPath = CombineDataPath(dataPath, modelPath);
                if (!File.Exists(fullPath))
                {
                    missingAssets.Add(fullPath);
                    _logger.Warning("Mesh file {Path} not found for ArmorAddon {Addon}", fullPath, addon.EditorID);
                    continue;
                }

                var identity = $"{variantForAddon}:{fullPath}";
                if (!visitedParts.Add(identity))
                {
                    continue; // Avoid loading identical meshes multiple times
                }

                var partName = $"{armorName} ({addon.EditorID ?? addon.FormKey.ToString()})";
                meshes.AddRange(LoadMeshesFromNif(partName, fullPath, dataPath, variantForAddon, cancellationToken));
            }
        }

        return new ArmorPreviewScene(gender, meshes, missingAssets);
    }

    private GenderedModelVariant DetermineEffectiveGender(
        IReadOnlyList<ArmorRecordViewModel> pieces,
        GenderedModelVariant preferredGender,
        ILinkCache linkCache)
    {
        if (preferredGender == GenderedModelVariant.Male)
            return GenderedModelVariant.Male;

        foreach (var piece in pieces)
        {
            foreach (var addonLink in piece.Armor.Armature)
            {
                if (!linkCache.TryResolve<IArmorAddonGetter>(addonLink.FormKey, out var addon) || addon is null)
                    continue;

                var worldModel = addon.WorldModel;
                if (worldModel == null)
                    continue;

                if (worldModel.Female != null)
                    continue;

                if (worldModel.Male != null)
                    return GenderedModelVariant.Male;
            }
        }

        return GenderedModelVariant.Female;
    }

    private IEnumerable<PreviewMeshShape> LoadMeshesFromNif(
        string partName,
        string fullPath,
        string dataPath,
        GenderedModelVariant variant,
        CancellationToken cancellationToken)
    {
        var meshes = new List<PreviewMeshShape>();
        var nif = new NifFile();

        try
        {
            var loadResult = nif.Load(fullPath);
            if (loadResult != 0 || !nif.Valid)
                return meshes;

            foreach (var shape in nif.GetShapes().OfType<INiShape>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!TryExtractMesh(nif, shape, dataPath, out var meshData))
                    continue;

                var shapeName = shape.Name?.ToString();
                var name = string.IsNullOrWhiteSpace(shapeName) ? partName : $"{partName} - {shapeName}";
                meshes.Add(new PreviewMeshShape(
                    name,
                    fullPath,
                    variant,
                    meshData.Vertices,
                    meshData.Normals,
                    meshData.Indices,
                    meshData.Transform,
                    meshData.DiffuseTexturePath));
            }
        }
        catch
        {
            // Swallow individual mesh issues and continue,
            // caller already logs missing files.
        }

        return meshes;
    }

    private bool TryExtractMesh(NifFile nif, INiShape shape, string dataPath, out MeshData meshData)
    {
        meshData = default;

        var vertices = ExtractVertices(shape);
        if (vertices == null || vertices.Count == 0)
            return false;

        var indices = ExtractIndices(shape);
        if (indices == null || indices.Count == 0)
            return false;

        var normals = ExtractNormals(shape);
        if (normals == null || normals.Count != vertices.Count)
        {
            normals = ComputeNormals(vertices, indices);
        }

        var transform = ComputeWorldTransform(nif, shape);
        var diffuse = ExtractDiffuseTexturePath(nif, shape, dataPath);

        if (!string.IsNullOrWhiteSpace(diffuse))
        {
            _logger.Debug("Shape {ShapeName} diffuse: {Texture}", shape.Name?.ToString() ?? "<unnamed>", diffuse);
        }

        meshData = new MeshData(vertices, normals, indices, transform, diffuse);
        return true;
    }

    private static List<Vector3>? ExtractVertices(INiShape shape)
    {
        switch (shape)
        {
            case BSTriShape bsTriShape when bsTriShape.VertexPositions != null:
                return bsTriShape.VertexPositions.Select(v => v).ToList();
            case NiTriShape niTriShape:
                var data = niTriShape.GeometryData;
                if (data?.Vertices != null)
                    return data.Vertices.Select(v => v).ToList();
                break;
        }

        return null;
    }

    private static List<int>? ExtractIndices(INiShape shape)
    {
        IEnumerable<NiflySharp.Structs.Triangle>? triangles = null;

        switch (shape)
        {
            case BSTriShape bsTriShape when bsTriShape.Triangles != null:
                triangles = bsTriShape.Triangles;
                break;
            case NiTriShape niTriShape:
                triangles = niTriShape.Triangles ?? niTriShape.GeometryData?.Triangles;
                break;
        }

        if (triangles == null)
            return null;

        var result = new List<int>();
        foreach (var tri in triangles)
        {
            result.Add(tri.V1);
            result.Add(tri.V2);
            result.Add(tri.V3);
        }

        return result;
    }

    private static List<Vector3>? ExtractNormals(INiShape shape)
    {
        switch (shape)
        {
            case BSTriShape bsTriShape when bsTriShape.Normals != null && bsTriShape.Normals.Count > 0:
                return bsTriShape.Normals.Select(n => n).ToList();
            case NiTriShape niTriShape:
                var data = niTriShape.GeometryData;
                if (data?.Normals != null && data.Normals.Count > 0)
                    return data.Normals.Select(n => n).ToList();
                break;
        }

        return null;
    }

    private static List<Vector3> ComputeNormals(IReadOnlyList<Vector3> vertices, IReadOnlyList<int> indices)
    {
        var normals = Enumerable.Repeat(Vector3.Zero, vertices.Count).ToList();

        for (int i = 0; i < indices.Count; i += 3)
        {
            var i0 = indices[i];
            var i1 = indices[i + 1];
            var i2 = indices[i + 2];

            if (i0 >= vertices.Count || i1 >= vertices.Count || i2 >= vertices.Count)
                continue;

            var a = vertices[i0];
            var b = vertices[i1];
            var c = vertices[i2];

            var normal = Vector3.Cross(b - a, c - a);
            if (normal != Vector3.Zero)
                normal = Vector3.Normalize(normal);

            normals[i0] += normal;
            normals[i1] += normal;
            normals[i2] += normal;
        }

        for (int i = 0; i < normals.Count; i++)
        {
            if (normals[i] != Vector3.Zero)
                normals[i] = Vector3.Normalize(normals[i]);
            else
                normals[i] = Vector3.UnitZ;
        }

        return normals;
    }

    private static Matrix4x4 ComputeWorldTransform(NifFile nif, INiShape shape)
    {
        Matrix4x4 world = Matrix4x4.Identity;
        const int MaxDepth = 256;

        INiObject? current = shape;
        var depth = 0;

        while (current is NiAVObject avObject)
        {
            var local = CreateLocalTransform(avObject);
            world = Matrix4x4.Multiply(local, world);

            current = nif.GetParentBlock(avObject);
            depth++;
            if (depth > MaxDepth)
                break;
        }

        return world;
    }

    private static Matrix4x4 CreateLocalTransform(NiAVObject avObject)
    {
        var scale = avObject.Scale == 0 ? 1f : avObject.Scale;
        var scaleMatrix = Matrix4x4.CreateScale(scale);

        var rot = avObject.Rotation;
        var rotationMatrix = new Matrix4x4(
            rot.M11, rot.M12, rot.M13, 0,
            rot.M21, rot.M22, rot.M23, 0,
            rot.M31, rot.M32, rot.M33, 0,
            0, 0, 0, 1);

        var translationMatrix = Matrix4x4.CreateTranslation(avObject.Translation);

        var result = Matrix4x4.Multiply(scaleMatrix, rotationMatrix);
        result = Matrix4x4.Multiply(result, translationMatrix);
        return result;
    }

    private string? ExtractDiffuseTexturePath(NifFile nif, INiShape shape, string dataPath)
    {
        string? texturePath = null;

        var primaryShader = nif.GetBlock<BSLightingShaderProperty>(shape.ShaderPropertyRef);
        texturePath = TryGetTextureFromShader(nif, primaryShader); 

        if (string.IsNullOrWhiteSpace(texturePath) && shape.Properties != null)
        {
            foreach (var propRef in shape.Properties.References)
            {
                var shader = nif.GetBlock<BSLightingShaderProperty>(propRef);
                texturePath = TryGetTextureFromShader(nif, shader);
                if (!string.IsNullOrWhiteSpace(texturePath))
                {
                    _logger.Debug("Resolved fallback texture {Texture} for shape {Shape}", texturePath, shape.Name?.ToString() ?? "<unnamed>");
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(texturePath))
            return null;

        var normalized = texturePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);

        var combined = Path.Combine(dataPath, normalized);
        _logger.Debug("Combined texture path: {Combined}", combined);
        return combined;
    }

    private static string? TryGetTextureFromShader(NifFile nif, BSLightingShaderProperty? shader)
    {
        if (shader == null)
            return null;

        if (shader.TextureSetRef == null || shader.TextureSetRef.IsEmpty())
            return null;

        var set = nif.GetBlock<BSShaderTextureSet>(shader.TextureSetRef);
        var diffuse = set?.Textures?.FirstOrDefault();
        var content = diffuse?.Content;
        if (!string.IsNullOrWhiteSpace(content))
            return content;

        return diffuse?.ToString();
    }

    private static IModelGetter? SelectModel(
        IGenderedItemGetter<IModelGetter?>? worldModel,
        GenderedModelVariant preferred,
        out GenderedModelVariant resolvedVariant)
    {
        resolvedVariant = preferred;

        if (worldModel == null)
            return null;

        if (preferred == GenderedModelVariant.Female)
        {
            if (worldModel.Female != null)
            {
                resolvedVariant = GenderedModelVariant.Female;
                return worldModel.Female;
            }

            if (worldModel.Male != null)
            {
                resolvedVariant = GenderedModelVariant.Male;
                return worldModel.Male;
            }
        }
        else
        {
            if (worldModel.Male != null)
            {
                resolvedVariant = GenderedModelVariant.Male;
                return worldModel.Male;
            }

            if (worldModel.Female != null)
            {
                resolvedVariant = GenderedModelVariant.Female;
                return worldModel.Female;
            }
        }

        return worldModel.Male ?? worldModel.Female;
    }

    private static string? ResolveModelPath(ISimpleModelGetter model)
    {
        var file = model.File;
        if (file == null)
            return null;

        if (!string.IsNullOrWhiteSpace(file.DataRelativePath.Path))
            return NormalizeRelativePath(file.DataRelativePath.Path);

        if (!string.IsNullOrWhiteSpace(file.GivenPath))
            return NormalizeRelativePath(file.GivenPath);

        return null;
    }

    private static string CombineDataPath(string dataPath, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
            return relativePath;

        return Path.Combine(dataPath, relativePath);
    }

    private static string GetBodyPath(string dataPath, GenderedModelVariant gender)
    {
        var relative = gender == GenderedModelVariant.Female ? FemaleBodyRelativePath : MaleBodyRelativePath;
        return Path.Combine(dataPath, relative);
    }

    private static string NormalizeRelativePath(string path)
    {
        var normalized = path.Replace('/', Path.DirectorySeparatorChar)
                             .Replace('\\', Path.DirectorySeparatorChar);
        return normalized.TrimStart(Path.DirectorySeparatorChar);
    }

    private readonly record struct MeshData(
        IReadOnlyList<Vector3> Vertices,
        IReadOnlyList<Vector3> Normals,
        IReadOnlyList<int> Indices,
        Matrix4x4 Transform,
        string? DiffuseTexturePath);
}
