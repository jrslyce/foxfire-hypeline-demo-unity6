#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
namespace Foxfire.Editor
{
public sealed class WardenAtlasImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Foxfire/Resources/HollowrootWarden/")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true; importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false; importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
        importer.isReadable = false;
    }
}
}
#endif
