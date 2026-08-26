using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class CodeBlockBoard : MonoBehaviour
{
    public static CodeBlockBoard Instance { get; private set; }

    [Header("Catalog")]
    public CodeBlockCatalog catalog;

    [Header("Layout")]
    public int columns = 5;
    public float columnSpacing = 0.34f;
    public float rowSpacing = 0.34f;

    [Header("Manual Setup")]
    [Tooltip("Optional slot anchors placed on the board. When set, blocks spawn at these transforms instead of an auto grid.")]
    public Transform[] slotAnchors;

    [Header("Board Visual")]
    [Tooltip("Optional prefab for the wall board (e.g. cork board). Skipped when a visual child already exists.")]
    public GameObject boardVisualPrefab;
    public bool respectExistingBoardVisual = true;
    public bool useProceduralFallback = true;
    public Vector3 boardVisualLocalPosition = Vector3.zero;
    public Vector3 boardVisualLocalEulerAngles = Vector3.zero;
    public Vector3 boardVisualLocalScale = Vector3.one;
    public Color boardColor = new Color(0.18f, 0.22f, 0.16f, 1f);
    public Color frameColor = new Color(0.35f, 0.28f, 0.18f, 1f);

    private readonly List<CodeBlockSlot> slots = new List<CodeBlockSlot>();
    private Transform slotsParent;

    private void Awake()
    {
        Instance = this;

        if (catalog == null)
            catalog = Resources.Load<CodeBlockCatalog>("CodeBlockCatalog");

        if (catalog == null)
            Debug.LogError("[CodeBlockBoard] CodeBlockCatalog not found. Place it at Assets/Resources/CodeBlockCatalog.asset");

        BuildBoardVisual();
        BuildSlots();
        InitializePool();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool ReturnBlock(Code code)
    {
        if (code == null)
            return false;

        if (code.GetComponent<CodeBlockShelfInstance>() != null)
            return true;

        var poolItem = code.GetComponent<CodeBlockPoolItem>();
        if (poolItem == null || poolItem.sourcePrefab == null)
        {
            if (!catalog.TryGetEntryForGameObject(code.gameObject, out var entry) || entry.prefab == null)
                return false;

            poolItem = code.gameObject.AddComponent<CodeBlockPoolItem>();
            poolItem.sourcePrefab = entry.prefab;
        }

        foreach (var slot in slots)
        {
            if (slot.blockPrefab != poolItem.sourcePrefab || !slot.IsEmpty)
                continue;

            return slot.PlaceBlock(code.gameObject);
        }

        return false;
    }

    public void ClearWorkspace()
    {
        var connectionManager = ConnectionManager.Instance;
        var codes = FindObjectsOfType<Code>();
        var blocksToReturn = new List<Code>();

        foreach (var code in codes)
        {
            if (code.GetComponent<CodeBlockShelfInstance>() != null)
                continue;

            blocksToReturn.Add(code);
        }

        foreach (var code in blocksToReturn)
        {
            connectionManager?.CleanupBlock(code);

            if (!ReturnBlock(code))
                Destroy(code.gameObject);
        }
    }

    private void InitializePool()
    {
        if (catalog == null)
            return;

        ValidateCatalogCounts();
    }

    private bool IsUnderBoard(Transform target)
    {
        return target != null && (target == transform || target.IsChildOf(transform));
    }

    private void BuildBoardVisual()
    {
        if (transform.Find("BoardVisual") != null)
            return;

        if (respectExistingBoardVisual && HasExistingBoardVisual())
            return;

        if (boardVisualPrefab != null)
        {
            var visual = Instantiate(boardVisualPrefab, transform);
            visual.name = "BoardVisual";
            visual.transform.localPosition = boardVisualLocalPosition;
            visual.transform.localEulerAngles = boardVisualLocalEulerAngles;
            visual.transform.localScale = boardVisualLocalScale;
            return;
        }

        if (useProceduralFallback)
            BuildProceduralBoard();
    }

    private bool HasExistingBoardVisual()
    {
        foreach (Transform child in transform)
        {
            if (child.name == "Slots")
                continue;

            if (child.GetComponentInChildren<Renderer>() != null)
                return true;
        }

        return false;
    }

    private void BuildProceduralBoard()
    {
        if (transform.Find("BoardSurface") != null)
            return;

        if (catalog == null || catalog.EntryCount == 0)
        {
            CreateQuad("BoardSurface", new Vector3(0f, 0f, -0.03f),
                new Vector3(2.2f, 1.4f, 1f),
                Quaternion.Euler(0f, 180f, 0f), boardColor);
            return;
        }

        int rows = Mathf.CeilToInt(catalog.EntryCount / (float)columns);
        float boardWidth = (columns - 1) * columnSpacing + 0.5f;
        float boardHeight = (rows - 1) * rowSpacing + 0.5f;

        CreateQuad("BoardSurface", new Vector3(0f, 0f, -0.02f),
            new Vector3(boardWidth + 0.3f, boardHeight + 0.3f, 1f),
            Quaternion.Euler(0f, 180f, 0f), boardColor);

        float frameThickness = 0.06f;
        float halfW = (boardWidth + 0.3f) * 0.5f;
        float halfH = (boardHeight + 0.3f) * 0.5f;

        CreateQuad("FrameTop", new Vector3(0f, halfH + frameThickness * 0.5f, -0.01f),
            new Vector3(boardWidth + 0.3f + frameThickness * 2f, frameThickness, 1f),
            Quaternion.identity, frameColor);
        CreateQuad("FrameBottom", new Vector3(0f, -halfH - frameThickness * 0.5f, -0.01f),
            new Vector3(boardWidth + 0.3f + frameThickness * 2f, frameThickness, 1f),
            Quaternion.identity, frameColor);
        CreateQuad("FrameLeft", new Vector3(-halfW - frameThickness * 0.5f, 0f, -0.01f),
            new Vector3(frameThickness, boardHeight + 0.3f, 1f),
            Quaternion.identity, frameColor);
        CreateQuad("FrameRight", new Vector3(halfW + frameThickness * 0.5f, 0f, -0.01f),
            new Vector3(frameThickness, boardHeight + 0.3f, 1f),
            Quaternion.identity, frameColor);
    }

    private void CreateQuad(string name, Vector3 localPos, Vector3 scale, Quaternion localRot, Color color)
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        quad.transform.SetParent(transform, false);
        quad.transform.localPosition = localPos;
        quad.transform.localRotation = localRot;
        quad.transform.localScale = scale;

        var collider = quad.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        var renderer = quad.GetComponent<Renderer>();
        if (renderer != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            renderer.material = new Material(shader) { color = color };
        }
    }

    private void BuildSlots()
    {
        slots.Clear();

        if (catalog == null || catalog.EntryCount == 0)
            return;

        EnsureSlotsParent();

        if (TryBuildSlotsFromSceneBlocks())
        {
            ValidateCatalogCounts();
            return;
        }

        if (slotAnchors != null && slotAnchors.Length > 0)
        {
            BuildSlotsFromAnchors();
            SpawnRuntimeBlocksForEmptySlots();
            return;
        }

        BuildGeneratedSlots();
        SpawnRuntimeBlocksForEmptySlots();
    }

    private void EnsureSlotsParent()
    {
        slotsParent = transform.Find("Slots");
        if (slotsParent == null)
        {
            var slotsObject = new GameObject("Slots");
            slotsParent = slotsObject.transform;
            slotsParent.SetParent(transform, false);
        }
    }

    private bool TryBuildSlotsFromSceneBlocks()
    {
        var sceneBlocks = CollectBoardCodeBlocks();
        if (sceneBlocks.Count == 0)
            return false;

        int slotIndex = 0;
        foreach (var code in sceneBlocks)
        {
            if (!catalog.TryGetEntryForGameObject(code.gameObject, out var entry) || entry.prefab == null)
            {
                Debug.LogWarning($"[CodeBlockBoard] Could not match scene block '{code.name}' to CodeBlockCatalog.", code);
                continue;
            }

            var slotObject = new GameObject($"Slot_{entry.displayName}_{slotIndex}");
            slotObject.transform.SetParent(slotsParent, true);
            slotObject.transform.SetPositionAndRotation(code.transform.position, code.transform.rotation);

            var slot = slotObject.AddComponent<CodeBlockSlot>();
            slot.blockPrefab = entry.prefab;
            slot.displayName = entry.displayName;
            slot.board = this;
            slot.RegisterPlacedBlock(code.gameObject);
            slots.Add(slot);
            slotIndex++;
        }

        return slots.Count > 0;
    }

    private List<Code> CollectBoardCodeBlocks()
    {
        var results = new List<Code>();
        var codes = GetComponentsInChildren<Code>(true);

        foreach (var code in codes)
        {
            if (code.transform == transform)
                continue;

            if (code.GetComponentInParent<CodeBlockBoard>() != this)
                continue;

            results.Add(code);
        }

        return results;
    }

    private void ValidateCatalogCounts()
    {
        var counts = new Dictionary<GameObject, int>();

        foreach (var slot in slots)
        {
            if (slot.blockPrefab == null)
                continue;

            if (!counts.ContainsKey(slot.blockPrefab))
                counts[slot.blockPrefab] = 0;

            counts[slot.blockPrefab]++;
        }

        for (int i = 0; i < catalog.EntryCount; i++)
        {
            var entry = catalog.GetEntry(i);
            if (entry == null || entry.prefab == null)
                continue;

            counts.TryGetValue(entry.prefab, out int sceneCount);

            if (sceneCount != entry.maxCount)
            {
                Debug.LogWarning(
                    $"[CodeBlockBoard] '{entry.displayName}' has {sceneCount} block(s) on the board but CodeBlockCatalog maxCount is {entry.maxCount}.");
            }
        }
    }

    private void BuildSlotsFromAnchors()
    {
        var expandedSlots = BuildExpandedSlotEntries();
        int anchorCount = Mathf.Min(expandedSlots.Count, slotAnchors.Length);

        for (int i = 0; i < anchorCount; i++)
        {
            var entry = expandedSlots[i];
            if (slotAnchors[i] == null)
                continue;

            CreateSlotAt(slotAnchors[i], entry.prefab, entry.displayName);
        }
    }

    private void BuildGeneratedSlots()
    {
        var expandedSlots = BuildExpandedSlotEntries();
        int rows = Mathf.CeilToInt(expandedSlots.Count / (float)columns);

        for (int i = 0; i < expandedSlots.Count; i++)
        {
            var entry = expandedSlots[i];
            int row = i / columns;
            int col = i % columns;

            float x = (col - (columns - 1) * 0.5f) * columnSpacing;
            float y = ((rows - 1) * 0.5f - row) * rowSpacing;

            var slotObject = new GameObject($"Slot_{entry.displayName}_{i}");
            slotObject.transform.SetParent(slotsParent, false);
            slotObject.transform.localPosition = new Vector3(x, y, 0.12f);
            slotObject.transform.localRotation = Quaternion.identity;

            CreateSlotAt(slotObject.transform, entry.prefab, entry.displayName);
        }
    }

    private List<CodeBlockEntry> BuildExpandedSlotEntries()
    {
        var expanded = new List<CodeBlockEntry>();

        for (int i = 0; i < catalog.EntryCount; i++)
        {
            var entry = catalog.GetEntry(i);
            if (entry == null || entry.prefab == null)
                continue;

            int count = Mathf.Max(1, entry.maxCount);
            for (int copy = 0; copy < count; copy++)
                expanded.Add(entry);
        }

        return expanded;
    }

    private void SpawnRuntimeBlocksForEmptySlots()
    {
        foreach (var slot in slots)
        {
            if (!slot.IsEmpty || slot.blockPrefab == null)
                continue;

            var block = Instantiate(slot.blockPrefab, slot.transform.position, slot.transform.rotation);
            block.transform.localScale = slot.blockPrefab.transform.localScale;
            slot.RegisterPlacedBlock(block);
        }
    }

    private void CreateSlotAt(Transform anchor, GameObject blockPrefab, string entryDisplayName)
    {
        var slotHost = anchor.gameObject;
        var slot = slotHost.GetComponent<CodeBlockSlot>();
        if (slot == null)
            slot = slotHost.AddComponent<CodeBlockSlot>();

        slot.blockPrefab = blockPrefab;
        slot.displayName = entryDisplayName;
        slot.board = this;
        slots.Add(slot);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 0.3f, 0.35f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(new Vector3(0f, 0f, 0.06f), new Vector3(1.9f, 1.2f, 0.08f));
    }
}
