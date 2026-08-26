using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Level Data")]
    public List<LevelData> levels;

    [Header("Prefabs")]
    public GameObject gridTileLightPrefab;
    public GameObject gridTileDarkPrefab;
    public GameObject starPrefab;

    [Header("Grid")]
    public Vector2Int gridSize;
    public Vector3 gridCenter = Vector3.zero;

    [Header("Settings")]
    public float cellSize = 1f;
    public float starHeight = 0.5f;

    public int nextStarIndex { get; private set; }
    public int currentLevelIndex { get; private set; }
    public LevelData currentLevelData => levels[currentLevelIndex];
    public bool IsLevelActive => levelActive;

    private GameObject gridParent;
    private MenuManager menu;
    private CodeManager codeManager;
    private RobotFacingIndicator facingIndicator;
    private bool levelActive;

    private void Awake()
    {
        Instance = this;
        menu = FindObjectOfType<MenuManager>();
        codeManager = FindObjectOfType<CodeManager>();
        facingIndicator = GetComponent<RobotFacingIndicator>();
        if (facingIndicator == null)
            facingIndicator = gameObject.AddComponent<RobotFacingIndicator>();
    }

    private void Start()
    {
        GeneratePlayground();
    }

    private void GeneratePlayground()
    {
        Vector2Int size = new Vector2Int(10, 10);
        GenerateGrid(size);

        if (CodeManager.Robot != null)
        {
            Vector3 origin = GridOrigin(size);
            Vector3 pos = origin + new Vector3(4 * cellSize + cellSize * 0.5f, 0f, 4 * cellSize + cellSize * 0.5f);
            CodeManager.Robot.transform.position = new Vector3(pos.x, gridCenter.y, pos.z);
            CodeManager.Robot.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        }
    }

    public void LoadLevelByIndex(int index)
    {
        if (index < 0 || index >= levels.Count) return;

        if (codeManager != null && codeManager.IsExecuting)
            codeManager.StopExecution();

        CodeBlockBoard.Instance?.ClearWorkspace();
        ClearLevel();

        currentLevelIndex = index;
        nextStarIndex = 0;

        var data = levels[index];
        var size = gridSize.x > 0 && gridSize.y > 0 ? gridSize : data.gridSize;

        GenerateGrid(size);
        SpawnStars(data, size);
        PlaceRobot(data, size);
        levelActive = true;
        facingIndicator?.Show();
        LevelBlockHintDisplay.Instance?.Show(data);
    }

    public void ReloadLevel()
    {
        LoadLevelByIndex(currentLevelIndex);
    }

    public void StopLevel()
    {
        levelActive = false;
        facingIndicator?.Hide();
        LevelBlockHintDisplay.Instance?.Hide();
        if (codeManager != null && codeManager.IsExecuting)
            codeManager.StopExecution();
        CodeBlockBoard.Instance?.ClearWorkspace();
        ClearLevel();
        GeneratePlayground();
    }

    public bool IsWithinGrid(Vector3 worldPos)
    {
        Vector2Int size = levelActive
            ? (gridSize.x > 0 && gridSize.y > 0 ? gridSize : currentLevelData.gridSize)
            : new Vector2Int(10, 10);

        Vector3 origin = GridOrigin(size);
        float halfCell = cellSize * 0.5f;

        return worldPos.x >= origin.x + halfCell
            && worldPos.x <= origin.x + size.x * cellSize - halfCell
            && worldPos.z >= origin.z + halfCell
            && worldPos.z <= origin.z + size.y * cellSize - halfCell;
    }

    public void CollectStar(Star star)
    {
        if (!levelActive) return;

        if (star.orderIndex != nextStarIndex)
        {
            menu?.ShowLevelFailed();
            return;
        }

        star.Collect();
        nextStarIndex++;

        if (nextStarIndex >= CountStarsInLevel())
        {
            menu?.ShowLevelComplete();
        }
    }

    private int CountStarsInLevel()
    {
        if (currentLevelIndex >= levels.Count) return 0;
        return levels[currentLevelIndex].starPositions.Length;
    }

    private void ClearLevel()
    {
        if (gridParent != null) Destroy(gridParent);
        foreach (var star in FindObjectsOfType<Star>())
            Destroy(star.gameObject);
    }

    private void GenerateGrid(Vector2Int size)
    {
        gridParent = new GameObject("Grid");
        Vector3 origin = GridOrigin(size);

        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                GameObject tilePrefab = (x + y) % 2 == 0 ? gridTileLightPrefab : gridTileDarkPrefab;
                Vector3 pos = origin + new Vector3(x * cellSize + cellSize * 0.5f, 0f, y * cellSize + cellSize * 0.5f);

                if (tilePrefab != null)
                {
                    var tile = Instantiate(tilePrefab, pos, Quaternion.Euler(90f, 0f, 0f), gridParent.transform);
                    tile.transform.localScale = Vector3.one * cellSize * 0.95f;
                }
            }
        }
    }

    private void SpawnStars(LevelData data, Vector2Int size)
    {
        Vector3 origin = GridOrigin(size);

        for (int i = 0; i < data.starPositions.Length; i++)
        {
            Vector3 pos = origin + new Vector3(data.starPositions[i].x * cellSize + cellSize * 0.5f, 0f, data.starPositions[i].y * cellSize + cellSize * 0.5f);
            if (starPrefab != null)
            {
                var star = Instantiate(starPrefab, new Vector3(pos.x, gridCenter.y + starHeight, pos.z), Quaternion.identity);
                var starComp = star.GetComponent<Star>();
                if (starComp != null) starComp.orderIndex = i;
            }
        }
    }

    private void PlaceRobot(LevelData data, Vector2Int size)
    {
        if (CodeManager.Robot == null) return;

        Vector3 origin = GridOrigin(size);
        Vector3 pos = origin + new Vector3(data.robotStart.x * cellSize + cellSize * 0.5f, 0f, data.robotStart.y * cellSize + cellSize * 0.5f);
        CodeManager.Robot.transform.position = new Vector3(pos.x, gridCenter.y, pos.z);

        float facingAngle = data.robotFacing switch
        {
            RobotDirection.North => 0f,
            RobotDirection.East => 90f,
            RobotDirection.South => 180f,
            RobotDirection.West => 270f,
            _ => 0f
        };
        CodeManager.Robot.transform.rotation = Quaternion.Euler(0f, facingAngle, 0f);
    }

    private Vector3 GridOrigin(Vector2Int size)
    {
        return gridCenter - new Vector3(size.x * cellSize * 0.5f, 0f, size.y * cellSize * 0.5f);
    }
}
