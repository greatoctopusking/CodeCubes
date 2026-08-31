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

    public GameObject GetPrefab(CodeBlockEntry entry)
    {
        return entry == null ? null : BlockIdentity.AsGameObject(entry.prefab);
    }

    public bool TryGetEntryForPrefab(GameObject prefab, out CodeBlockEntry entry)
    {
        entry = null;
        prefab = BlockIdentity.AsGameObject(prefab);
        if (prefab == null || entries == null)
            return false;

        foreach (var candidate in entries)
        {
            var candidatePrefab = GetPrefab(candidate);
            if (candidatePrefab == null)
                continue;

            if (candidatePrefab == prefab || BlockIdentity.Matches(prefab, candidatePrefab))
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
            if (candidate == null)
                continue;

            if (BlockIdentity.NamesMatch(blockObject.name, candidate.displayName))
            {
                entry = candidate;
                return true;
            }

            var candidatePrefab = GetPrefab(candidate);
            if (candidatePrefab != null && BlockIdentity.Matches(blockObject, candidatePrefab))
            {
                entry = candidate;
                return true;
            }
        }

        return false;
    }
}
