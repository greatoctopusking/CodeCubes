using UnityEngine;

[CreateAssetMenu(fileName = "Level", menuName = "VRPG/Level Data")]
public class LevelData : ScriptableObject
{
    public int levelNumber;
    public Vector2Int gridSize = new(10, 10);
    public Vector2Int robotStart;
    public RobotDirection robotFacing = RobotDirection.North;
    public Vector2Int[] starPositions;

    [Header("Hints")]
    [Tooltip("Optional. Shown on the in-level screen until the player runs code. Falls back to LevelBlockHints if empty.")]
    public string[] suggestedBlockNames;
}

public enum RobotDirection { North, East, South, West }
