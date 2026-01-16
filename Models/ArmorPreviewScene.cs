using System.Numerics;

namespace Boutique.Models;

public enum GenderedModelVariant
{
    Female,
    Male
}

public sealed record PreviewMeshShape(
    string Name,
    string SourcePath,
    GenderedModelVariant Variant,
    IReadOnlyList<Vector3> Vertices,
    IReadOnlyList<Vector3> Normals,
    IReadOnlyList<Vector2>? TextureCoordinates,
    IReadOnlyList<int> Indices,
    Matrix4x4 Transform,
    string? DiffuseTexturePath);

public sealed record ArmorPreviewScene(
    GenderedModelVariant Gender,
    IReadOnlyList<PreviewMeshShape> Meshes,
    IReadOnlyList<string> MissingAssets,
    string? OutfitLabel = null,
    string? SourceFile = null,
    bool IsWinner = false);

public sealed record OutfitMetadata(
    string? OutfitLabel,
    string? SourceFile,
    bool IsWinner);

public sealed class ArmorPreviewSceneCollection
{
    private readonly Dictionary<int, ArmorPreviewScene> _sceneCache = new();
    private readonly Func<int, Task<ArmorPreviewScene>> _sceneBuilder;

    public int Count { get; }
    public int InitialIndex { get; }
    public IReadOnlyList<OutfitMetadata> Metadata { get; }

    public ArmorPreviewSceneCollection(ArmorPreviewScene singleScene)
    {
        Count = 1;
        InitialIndex = 0;
        Metadata = new[] { new OutfitMetadata(singleScene.OutfitLabel, singleScene.SourceFile, singleScene.IsWinner) };
        _sceneCache[0] = singleScene;
        _sceneBuilder = _ => Task.FromResult(singleScene);
    }

    public ArmorPreviewSceneCollection(
        int count,
        int initialIndex,
        IReadOnlyList<OutfitMetadata> metadata,
        Func<int, Task<ArmorPreviewScene>> sceneBuilder)
    {
        Count = count;
        InitialIndex = initialIndex;
        Metadata = metadata;
        _sceneBuilder = sceneBuilder;
    }

    public async Task<ArmorPreviewScene> GetSceneAsync(int index)
    {
        if (_sceneCache.TryGetValue(index, out var cached))
            return cached;

        var scene = await _sceneBuilder(index);
        _sceneCache[index] = scene;
        return scene;
    }
}
