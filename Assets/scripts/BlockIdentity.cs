using System;
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

    public static bool NamesMatch(string left, string right)
    {
        return string.Equals(NormalizeName(left), NormalizeName(right), StringComparison.OrdinalIgnoreCase);
    }

    public static bool Matches(GameObject instance, GameObject prefab)
    {
        if (instance == null || prefab == null)
            return false;

        if (NamesMatch(instance.name, prefab.name))
            return true;

        var instanceCode = instance.GetComponent<Code>();
        var prefabCode = prefab.GetComponent<Code>();
        return instanceCode != null && prefabCode != null && instanceCode.GetType() == prefabCode.GetType();
    }

    public static GameObject AsGameObject(UnityEngine.Object asset)
    {
        if (asset == null)
            return null;

        if (asset is GameObject gameObject)
            return gameObject;

        if (asset is Component component)
            return component.gameObject;

        return null;
    }
}
