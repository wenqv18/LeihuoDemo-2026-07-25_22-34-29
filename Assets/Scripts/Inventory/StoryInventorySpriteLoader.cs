using UnityEngine;

public static class StoryInventorySpriteLoader
{
    public static Sprite Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite != null)
        {
            return sprite;
        }

#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
#else
        return null;
#endif
    }
}
