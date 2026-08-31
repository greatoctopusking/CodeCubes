using System;
using UnityEngine;

public static class BlockIdentity
{
    public static string NormalizeName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return string.Empty;

        string normalized = objectName.Replace("(Clone)", string.Empty).Trim();

        // Unity duplicate suffix: "name (1)"
        int asciiSuffix = normalized.IndexOf(" (", StringComparison.Ordinal);
        if (asciiSuffix > 0)
            normalized = normalized.Substring(0, asciiSuffix);

        // Blender / Chinese rename suffix: "turnleft（改）"
        int fullwidthSuffix = normalized.IndexOf('（');
        if (fullwidthSuffix > 0)
            normalized = normalized.Substring(0, fullwidthSuffix);

        return normalized.Trim();
    }

    public static bool NamesMatch(string left, string right)
    {
        return string.Equals(NormalizeName(left), NormalizeName(right), StringComparison.OrdinalIgnoreCase);
    }

    public static bool Matches(GameObject instance, GameObject prefab)
    {
        if (!instance || !prefab)
            return false;

        if (NamesMatch(instance.name, prefab.name))
            return true;

        // Type fallback without GetComponent on prefab asset (can throw MissingReference in batch/Edit Mode).
        var instanceCode = instance.GetComponent<Code>();
        if (!instanceCode)
            return false;

        return TryGetPrefabNameForCode(instanceCode, out string expectedName) &&
               NamesMatch(expectedName, prefab.name);
    }

    public static bool TryGetPrefabNameForCode(Code code, out string prefabName)
    {
        prefabName = null;
        if (!code)
            return false;

        var type = code.GetType();
        if (type == typeof(Start)) { prefabName = "Start"; return true; }
        if (type == typeof(MoveCode)) { prefabName = "MoveForward"; return true; }
        if (type == typeof(TurnLeftCode)) { prefabName = "TurnLeft"; return true; }
        if (type == typeof(TurnRightCode)) { prefabName = "TurnRight"; return true; }
        if (type == typeof(While)) { prefabName = "While"; return true; }
        if (type == typeof(WhileEnd)) { prefabName = "WhileEnd"; return true; }
        if (type == typeof(If)) { prefabName = "IF"; return true; }
        if (type == typeof(IfEnd)) { prefabName = "IfEnd"; return true; }
        if (type == typeof(Else)) { prefabName = "Else"; return true; }
        if (type == typeof(TrueCode)) { prefabName = "True"; return true; }
        if (type == typeof(FalseCode)) { prefabName = "False"; return true; }
        if (type == typeof(StarFrontCode)) { prefabName = "DetectFrontStar"; return true; }
        if (type == typeof(StarLeftCode)) { prefabName = "DetectLeftStar"; return true; }
        if (type == typeof(StarRightCode)) { prefabName = "DetectRightStar"; return true; }
        if (type == typeof(StarRemainCode)) { prefabName = "StarRemain"; return true; }
        return false;
    }
}
