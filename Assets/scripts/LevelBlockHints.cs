public static class LevelBlockHints
{
    public static string[] GetForLevel(int levelNumber)
    {
        return levelNumber switch
        {
            1 => new[] { "Start", "MoveForward" },
            2 or 3 or 4 => new[] { "Start", "MoveForward", "TurnLeft", "TurnRight" },
            5 or 6 or 7 or 12 => new[] { "Start", "MoveForward", "TurnLeft", "TurnRight" },
            8 => new[] { "Start", "MoveForward", "While", "WhileEnd", "StarRemain" },
            9 => new[] { "Start", "MoveForward", "TurnLeft", "IF", "IfEnd", "DetectLeftStar" },
            10 => new[] { "Start", "MoveForward", "IF", "IfEnd", "DetectFrontStar", "While", "WhileEnd", "StarRemain" },
            11 => new[] { "Start", "MoveForward", "TurnLeft", "TurnRight", "IF", "IfEnd", "Else", "DetectLeftStar" },
            13 => new[] { "Start", "MoveForward", "While", "WhileEnd", "DetectFrontStar" },
            14 => new[] { "Start", "MoveForward", "While", "WhileEnd", "StarRemain" },
            15 or 18 => new[] { "Start", "MoveForward", "TurnLeft", "While", "WhileEnd", "DetectFrontStar" },
            16 or 17 or 19 or 20 => new[]
            {
                "Start", "MoveForward", "TurnLeft", "TurnRight",
                "While", "WhileEnd", "IF", "IfEnd", "Else",
                "DetectFrontStar", "DetectLeftStar", "DetectRightStar", "StarRemain"
            },
            _ => new[] { "Start", "MoveForward" }
        };
    }
}
