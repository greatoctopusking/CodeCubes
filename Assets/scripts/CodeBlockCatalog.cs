using System;
using UnityEngine;

[Serializable]
public class CodeBlockEntry
{
    [Tooltip("Shown in the Inspector for easier editing.")]
    public string displayName;

    public GameObject prefab;

    [Min(1)]
    [Tooltip("How many copies of this block exist in the pool (on the board + workspace combined).")]
    public int maxCount = 1;
}

[CreateAssetMenu(fileName = "CodeBlockCatalog", menuName = "VRPG/Code Block Catalog")]
public class CodeBlockCatalog : ScriptableObject
{
    public CodeBlockEntry[] entries;

    public int EntryCount => entries != null ? entries.Length : 0;

    public int TotalBlockCount
    {
        get
        {
            if (entries == null)
                return 0;

            int total = 0;
            foreach (var entry in entries)
            {
                if (entry == null || entry.prefab == null)
                    continue;

                total += Mathf.Max(1, entry.maxCount);
            }

            return total;
        }
    }

    public CodeBlockEntry GetEntry(int index)
    {
        if (entries == null || index < 0 || index >= entries.Length)
            return null;

        return entries[index];
    }

    public bool TryGetEntryForPrefab(GameObject prefab, out CodeBlockEntry entry)
    {
        entry = null;
        if (prefab == null || entries == null)
            return false;

        foreach (var candidate in entries)
        {
            if (candidate == null || candidate.prefab == null)
                continue;

            if (candidate.prefab == prefab || BlockIdentity.Matches(prefab, candidate.prefab))
            {
                entry = candidate;
                return true;
            }
        }

        return false;
    }

    public bool TryGetEntryForGameObject(GameObject blockObject, out CodeBlockEntry entry)
    {
        entry = null;
        if (blockObject == null || entries == null)
            return false;

        var poolItem = blockObject.GetComponent<CodeBlockPoolItem>();
        if (poolItem != null && poolItem.sourcePrefab != null)
            return TryGetEntryForPrefab(poolItem.sourcePrefab, out entry);

        foreach (var candidate in entries)
        {
            if (candidate == null || candidate.prefab == null)
                continue;

            if (BlockIdentity.Matches(blockObject, candidate.prefab))
            {
                entry = candidate;
                return true;
            }
        }

        return false;
    }
}
