public static class LevelBlockHints
{
    public static readonly string[] FullToolkit =
    {
        "Start", "MoveForward", "TurnLeft", "TurnRight",
        "While", "WhileEnd", "IF", "IfEnd", "Else",
        "DetectFrontStar", "DetectLeftStar", "DetectRightStar", "StarRemain"
    };

    public static string[] GetForLevel(int levelNumber)
    {
        return levelNumber switch
        {
            1 => new[] { "Start", "MoveForward" },
            2 => new[] { "Start", "MoveForward", "TurnLeft" },
            3 or 4 or 5 => new[] { "Start", "MoveForward", "TurnLeft", "TurnRight" },
            6 => new[] { "Start", "MoveForward", "While", "WhileEnd", "StarRemain" },
            7 => new[] { "Start", "MoveForward", "TurnLeft", "IF", "IfEnd", "DetectLeftStar" },
            8 => new[] { "Start", "MoveForward", "While", "WhileEnd", "DetectFrontStar" },
            9 => new[] { "Start", "MoveForward", "TurnLeft", "While", "WhileEnd", "DetectFrontStar" },
            10 or 11 or 12 => FullToolkit,
            _ => new[] { "Start", "MoveForward" }
        };
    }

    public static bool IsAllowed(string displayName, string[] suggested)
    {
        if (suggested == null || suggested.Length == 0)
            return true;

        if (BlockIdentity.NamesMatch(displayName, "Start"))
            return true;

        for (int i = 0; i < suggested.Length; i++)
        {
            if (BlockIdentity.NamesMatch(displayName, suggested[i]))
                return true;
        }

        return false;
    }

    public static int GetCopyCap(string displayName, string[] suggested)
    {
        if (suggested == null || suggested.Length == 0)
            return int.MaxValue;

        if (!HasControlFlow(suggested))
            return int.MaxValue;

        bool twoWhileSegments = !IsFullToolkit(suggested)
            && HasName(suggested, "While")
            && (HasName(suggested, "TurnLeft") || HasName(suggested, "TurnRight"));

        if (IsMovement(displayName))
        {
            if (IsFullToolkit(suggested))
                return 4;
            return twoWhileSegments ? 2 : 1;
        }

        if (IsPairedControl(displayName))
            return 2;

        if (BlockIdentity.NamesMatch(displayName, "Else"))
            return 2;

        if (twoWhileSegments && BlockIdentity.NamesMatch(displayName, "DetectFrontStar"))
            return 2;

        return 1;
    }

    public static bool ShouldRequireControlFlow(string[] suggested)
    {
        if (suggested == null || suggested.Length == 0)
            return false;

        if (IsFullToolkit(suggested))
            return false;

        return HasName(suggested, "IF") || HasName(suggested, "While");
    }

    public static bool HasName(string[] names, string blockName)
    {
        if (names == null)
            return false;

        for (int i = 0; i < names.Length; i++)
        {
            if (BlockIdentity.NamesMatch(names[i], blockName))
                return true;
        }

        return false;
    }

    private static bool HasControlFlow(string[] suggested)
    {
        return HasName(suggested, "While") || HasName(suggested, "IF");
    }

    private static bool IsFullToolkit(string[] suggested)
    {
        return HasName(suggested, "IF") && HasName(suggested, "While") && HasName(suggested, "Else");
    }

    private static bool IsMovement(string displayName)
    {
        return BlockIdentity.NamesMatch(displayName, "MoveForward")
            || BlockIdentity.NamesMatch(displayName, "TurnLeft")
            || BlockIdentity.NamesMatch(displayName, "TurnRight");
    }

    private static bool IsPairedControl(string displayName)
    {
        return BlockIdentity.NamesMatch(displayName, "While")
            || BlockIdentity.NamesMatch(displayName, "WhileEnd")
            || BlockIdentity.NamesMatch(displayName, "IF")
            || BlockIdentity.NamesMatch(displayName, "IfEnd");
    }
}
