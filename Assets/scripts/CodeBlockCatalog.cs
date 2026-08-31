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

        var code = blockObject.GetComponent<Code>();
        BlockIdentity.TryGetPrefabNameForCode(code, out string expectedName);

        // Prefer pool binding, but fall through if stale or unresolved.
        var poolItem = blockObject.GetComponent<CodeBlockPoolItem>();
        if (poolItem != null && poolItem.sourcePrefab != null)
        {
            if (TryGetEntryForPrefab(poolItem.sourcePrefab, out entry))
            {
                if (string.IsNullOrEmpty(expectedName) ||
                    BlockIdentity.NamesMatch(entry.displayName, expectedName) ||
                    (entry.prefab != null && BlockIdentity.NamesMatch(entry.prefab.name, expectedName)))
                {
                    return true;
                }

                // Stale pool binding (e.g. previous wrong match) — ignore and rematch.
                entry = null;
            }
        }

        // Robust path for Blender scene instances: Code subclass → catalog displayName.
        if (!string.IsNullOrEmpty(expectedName))
        {
            foreach (var candidate in entries)
            {
                if (candidate == null)
                    continue;

                if (!string.IsNullOrEmpty(candidate.displayName) &&
                    BlockIdentity.NamesMatch(candidate.displayName, expectedName))
                {
                    entry = candidate;
                    return true;
                }

                if (candidate.prefab != null && BlockIdentity.NamesMatch(candidate.prefab.name, expectedName))
                {
                    entry = candidate;
                    return true;
                }
            }
        }

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
