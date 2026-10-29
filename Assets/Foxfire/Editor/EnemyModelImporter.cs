using UnityEditor;
using UnityEngine;
namespace Foxfire.Editor
{
public sealed class EnemyModelImporter : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(EnemyModelBake.Source)) return;
        var importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = assetPath.Contains("Walking_withSkin");
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
    }
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(EnemyModelBake.Destination + "/")) return;
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
