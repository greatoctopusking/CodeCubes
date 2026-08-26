using UnityEngine;

public static class BlockIdentity
{
    public static string NormalizeName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return string.Empty;

        string normalized = objectName.Replace("(Clone)", string.Empty).Trim();
        int suffixIndex = normalized.IndexOf(" (");

        if (suffixIndex > 0)
            normalized = normalized.Substring(0, suffixIndex);

        return normalized;
    }

    public static bool Matches(GameObject instance, GameObject prefab)
    {
        if (instance == null || prefab == null)
            return false;

        return NormalizeName(instance.name) == prefab.name;
    }
}
