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
    [Tooltip("Blocks shown on the in-level screen. Only these types can be grabbed from the wall this level. Falls back to LevelBlockHints if empty.")]
    public string[] suggestedBlockNames;

    public string[] GetSuggestedBlockNames()
    {
        if (suggestedBlockNames != null && suggestedBlockNames.Length > 0)
            return suggestedBlockNames;

        return LevelBlockHints.GetForLevel(levelNumber);
    }
}

public enum RobotDirection { North, East, South, West }
