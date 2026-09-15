using UnityEditor;
using UnityEngine;

/// <summary>
/// 把玩家美术图片改为 Single 整图导入（移除多部件切片）。
/// 菜单：Tools/Player/Fix Player Sprite Import (Single)
/// </summary>
public static class PlayerSpriteImportFixer
{
    [MenuItem("Tools/Player/Fix Player Sprite Import (Single)")]
    public static void Fix()
    {
        FixTexture("Assets/Resources/Player/Player0.png");
        FixTexture("Assets/Resources/Player/Player1.png");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void FixTexture(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[PlayerSpriteImportFixer] Importer not found: {assetPath}");
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePivot = new Vector2(0.5f, 0.5f);
        importer.SaveAndReimport();
        Debug.Log($"[PlayerSpriteImportFixer] {assetPath} -> {importer.spriteImportMode}");
    }
}
