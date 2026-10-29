#if UNITY_EDITOR
using UnityEditor;

namespace Foxfire.Editor
{
    public sealed class FoxHeroAtlasImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (assetPath != "Assets/Foxfire/Resources/FoxHero/FoxWalk.png") return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 8192;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
            importer.isReadable = false;
        }
    }
}
#endif
