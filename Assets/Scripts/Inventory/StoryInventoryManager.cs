using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class StoryInventoryItemData
{
    public int id;
    public string imagePath;
    public string itemName;
    [TextArea] public string description;
}

public sealed class StoryInventoryManager : MonoBehaviour
{
    private const string SaveKey = "StoryInventory.Items";

    public static StoryInventoryManager Instance { get; private set; }
    public static event Action Changed;

    [SerializeField] private List<StoryInventoryItemData> itemDefinitions = new List<StoryInventoryItemData>();

    private readonly List<int> ownedItemIds = new List<int>();
    private bool loaded;

    public IReadOnlyList<int> OwnedItemIds
    {
        get
        {
            EnsureLoaded();
            return ownedItemIds;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureLoaded();
        StoryItemPresets.RegisterAll(this);
    }

    public static bool HasInstance => Instance != null;

    public static bool Add(int itemId)
    {
        return GetOrCreate().AddItem(itemId);
    }

    public static bool Has(int itemId)
    {
        return GetOrCreate().HasItem(itemId);
    }

    public static StoryInventoryManager GetOrCreateInstance()
    {
        return GetOrCreate();
    }

    public static StoryInventoryItemData GetData(int itemId)
    {
        return GetOrCreate().GetItemData(itemId);
    }

    public void RegisterItemData(StoryInventoryItemData data)
    {
        if (data == null || data.id < 0)
        {
            return;
        }

        for (int i = 0; i < itemDefinitions.Count; i++)
        {
            StoryInventoryItemData existing = itemDefinitions[i];
            if (existing != null && existing.id == data.id)
            {
                existing.itemName = data.itemName;
                existing.description = data.description;
                existing.imagePath = data.imagePath;
                return;
            }
        }

        itemDefinitions.Add(data);
    }

    public bool AddItem(int itemId)
    {
        EnsureLoaded();
        if (ownedItemIds.Contains(itemId))
        {
            return false;
        }

        ownedItemIds.Add(itemId);
        Save();
        Changed?.Invoke();
        return true;
    }

    public bool HasItem(int itemId)
    {
        EnsureLoaded();
        return ownedItemIds.Contains(itemId);
    }

    public List<int> CopyOwnedItemIds()
    {
        EnsureLoaded();
        return new List<int>(ownedItemIds);
    }

    public void ReplaceOwnedItems(IReadOnlyList<int> itemIds)
    {
        loaded = true;
        ownedItemIds.Clear();
        if (itemIds != null)
        {
            for (int i = 0; i < itemIds.Count; i++)
            {
                int itemId = itemIds[i];
                if (itemId >= 0 && !ownedItemIds.Contains(itemId))
                {
                    ownedItemIds.Add(itemId);
                }
            }
        }

        Save();
        Changed?.Invoke();
    }

    public void ClearOwnedItems()
    {
        EnsureLoaded();
        if (ownedItemIds.Count == 0)
        {
            Save();
            return;
        }

        ownedItemIds.Clear();
        Save();
        Changed?.Invoke();
    }

    public StoryInventoryItemData GetItemData(int itemId)
    {
        for (int i = 0; i < itemDefinitions.Count; i++)
        {
            StoryInventoryItemData item = itemDefinitions[i];
            if (item != null && item.id == itemId)
            {
                return item;
            }
        }

        return new StoryInventoryItemData
        {
            id = itemId,
            itemName = "Item " + itemId,
            description = string.Empty,
            imagePath = string.Empty
        };
    }

    private static StoryInventoryManager GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        StoryInventoryManager existing = FindAnyObjectByType<StoryInventoryManager>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject go = new GameObject(nameof(StoryInventoryManager));
        return go.AddComponent<StoryInventoryManager>();
    }

    private void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        ownedItemIds.Clear();
        if (!PlayerPrefs.HasKey(SaveKey))
        {
            return;
        }

        string json = PlayerPrefs.GetString(SaveKey);
        StoryInventorySaveData data = JsonUtility.FromJson<StoryInventorySaveData>(json);
        if (data != null && data.ids != null)
        {
            for (int i = 0; i < data.ids.Count; i++)
            {
                int itemId = data.ids[i];
                if (itemId >= 0 && !ownedItemIds.Contains(itemId))
                {
                    ownedItemIds.Add(itemId);
                }
            }
        }
    }

    private void Save()
    {
        StoryInventorySaveData data = new StoryInventorySaveData { ids = ownedItemIds };
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    [Serializable]
    private sealed class StoryInventorySaveData
    {
        public List<int> ids = new List<int>();
    }
}
