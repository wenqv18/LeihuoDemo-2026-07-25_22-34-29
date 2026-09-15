using UnityEngine;
using UnityEngine.SceneManagement;

public static class CorpseRuntimeSpawner
{
    private const int CorpseFloorNumber = 9;
    private const string SceneCorpseName = "PlayerCorpse";
    private const string ContainerName = "RuntimeCorpses";
    private const string CorpseSpritePath = "Player/Corpse/Death01";
    private const float CorpsePixelsPerUnit = 467f;
    private static Sprite corpseSprite;

    public static void Spawn(
        Scene scene,
        FloorSaveData floor,
        Transform normalRoot,
        Transform specialRoot)
    {
        if (TryConfigureSceneCorpse(scene, floor))
        {
            return;
        }

        if (floor == null || floor.corpses == null || floor.corpses.Count == 0)
        {
            return;
        }

        Player2DMovementController player = Object.FindAnyObjectByType<Player2DMovementController>();
        SpriteRenderer playerRenderer = player != null
            ? player.GetComponentInChildren<SpriteRenderer>()
            : null;

        bool spawnedAny = false;
        for (int i = 0; i < floor.corpses.Count; i++)
        {
            CorpseSaveData corpse = floor.corpses[i];
            if (corpse == null || FindExisting(scene, corpse.corpseId) != null)
            {
                continue;
            }

            WorldKind2D world;
            bool special = System.Enum.TryParse(corpse.world, out world) && world == WorldKind2D.Special;
            Transform worldRoot = special ? specialRoot : normalRoot;
            if (worldRoot == null)
            {
                continue;
            }

            Transform container = worldRoot.Find(ContainerName);
            if (container == null)
            {
                GameObject containerObject = new GameObject(ContainerName);
                containerObject.transform.SetParent(worldRoot, false);
                container = containerObject.transform;
            }

            GameObject corpseObject = new GameObject("Corpse_" + corpse.corpseId);
            corpseObject.transform.SetParent(container, false);
            corpseObject.transform.position = new Vector3(corpse.x, corpse.y, corpse.z);
            spawnedAny = true;
            // 尸体预制体可能已自带组件，避免重复挂载。
            if (corpseObject.GetComponent<CorpseFollowerAI>() == null)
            {
                corpseObject.AddComponent<CorpseFollowerAI>();
            }

            if (corpseObject.GetComponent<CorpseInteraction>() == null)
            {
                corpseObject.AddComponent<CorpseInteraction>();
            }

            if (!CreateCorpseVisual(corpseObject.transform, corpse, playerRenderer) &&
                player != null)
            {
                CreateFallbackPlayerVisual(corpseObject.transform, corpse, player);
            }
        }

        if (spawnedAny)
        {
            GameAudioManager.Instance?.PlayMonsterRoar();
        }
    }

    private static bool TryConfigureSceneCorpse(Scene scene, FloorSaveData floor)
    {
        GameObject sceneCorpse = FindSceneCorpse(scene);
        if (sceneCorpse == null)
        {
            return false;
        }

        bool shouldShow = floor != null &&
            floor.floorNumber == CorpseFloorNumber &&
            floor.corpses != null &&
            floor.corpses.Count > 0;

        sceneCorpse.SetActive(shouldShow);
        if (!shouldShow)
        {
            return true;
        }

        // Level_03 keeps a hand-authored corpse object. Do not override its sprite,
        // color, facing, scale, or motion setup here; artists tune those in-scene.

        if (sceneCorpse.GetComponent<CorpseInteraction>() == null)
        {
            sceneCorpse.AddComponent<CorpseInteraction>();
        }

        GameAudioManager.Instance?.PlayMonsterRoar();
        return true;
    }

    private static GameObject FindSceneCorpse(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == SceneCorpseName)
                {
                    return transforms[i].gameObject;
                }
            }
        }

        return null;
    }

    private static bool CreateCorpseVisual(
        Transform parent,
        CorpseSaveData corpse,
        SpriteRenderer sortingSource)
    {
        Sprite sprite = LoadCorpseSprite();
        if (sprite == null)
        {
            return false;
        }

        GameObject visual = new GameObject("CorpseDeath");
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(0.75f, 0.75f, 0.75f, 1f);
        renderer.flipX = corpse != null && corpse.facingLeft;
        if (sortingSource != null)
        {
            renderer.sortingLayerID = sortingSource.sortingLayerID;
            renderer.sortingOrder = sortingSource.sortingOrder - 1;
        }

        return true;
    }

    private static Sprite LoadCorpseSprite()
    {
        if (corpseSprite != null)
        {
            return corpseSprite;
        }

        Texture2D texture = Resources.Load<Texture2D>(CorpseSpritePath);
        if (texture == null)
        {
            return null;
        }

        corpseSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            CorpsePixelsPerUnit);
        corpseSprite.name = "CorpseDeath";
        return corpseSprite;
    }

    private static void CreateFallbackPlayerVisual(
        Transform parent,
        CorpseSaveData corpse,
        Player2DMovementController player)
    {
        SpriteRenderer[] sourceRenderers = player.GetComponentsInChildren<SpriteRenderer>(true);
        for (int rendererIndex = 0; rendererIndex < sourceRenderers.Length; rendererIndex++)
        {
            SpriteRenderer source = sourceRenderers[rendererIndex];
            if (source.sprite == null)
            {
                continue;
            }

            GameObject visual = new GameObject(source.name);
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = source.transform.localPosition;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = source.transform.localScale;
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = source.sprite;
            renderer.color = new Color(0.42f, 0.42f, 0.42f, 0.72f);
            renderer.flipX = corpse != null && corpse.facingLeft;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder - 1;
        }
    }

    private static GameObject FindExisting(Scene scene, string corpseId)
    {
        string expectedName = "Corpse_" + corpseId;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == expectedName)
                {
                    return transforms[i].gameObject;
                }
            }
        }

        return null;
    }
}
