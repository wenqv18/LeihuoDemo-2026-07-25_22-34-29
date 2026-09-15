using UnityEditor;

public sealed class Player0TransparentAnimationImporter : AssetPostprocessor
{
    private const string ArtFolder = "Assets/Art/Player0AnimationTransparent/";
    private const string ResourcesFolder = "Assets/Resources/Player/AnimationTransparent/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(ArtFolder) && !assetPath.StartsWith(ResourcesFolder))
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = UnityEngine.FilterMode.Bilinear;
        importer.spritePixelsPerUnit = 100;
    }
}
