using System.Collections;
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

    [Header("Start Block")]
    [Tooltip("Where the unique Start block is placed when a level opens.")]
    public Vector3 startDropPosition = new Vector3(-0.71f, 0.15f, 6.47f);
    public Vector3 startDropEulerAngles = new Vector3(0f, 270f, 0f);

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
    private readonly List<SceneBlockPose> sceneBlockPoses = new List<SceneBlockPose>();
    private Transform slotsParent;

    private struct SceneBlockPose
    {
        public Code code;
        public Transform parent;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
    }

    private void Awake()
    {
        Instance = this;

        if (catalog == null)
            catalog = Resources.Load<CodeBlockCatalog>("CodeBlockCatalog");

        if (catalog == null)
            Debug.LogError("[CodeBlockBoard] CodeBlockCatalog not found. Place it at Assets/Resources/CodeBlockCatalog.asset");

        CaptureSceneBlockPoses();
        DestroyGeneratedBoardVisuals();
        BuildSlots();
        RestoreSceneBlockPoses();
        RemoveLooseTurnBlocks();
        InitializePool();
    }

    private IEnumerator Start()
    {
        RestoreSceneBlockPoses();
        yield return null;
        RestoreSceneBlockPoses();
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
            if (!catalog.TryGetEntryForGameObject(code.gameObject, out var entry))
                return false;

            var prefab = catalog.GetPrefab(entry);
            if (prefab == null)
                return false;

            poolItem = code.gameObject.AddComponent<CodeBlockPoolItem>();
            poolItem.sourcePrefab = prefab;
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
        connectionManager?.ClearAllConnections();

        var codes = FindObjectsOfType<Code>(true);
        var blocksToDestroy = new List<Code>();

        foreach (var code in codes)
        {
            if (code == null)
                continue;

            if (code.GetComponentInParent<Code>() != code)
                continue;

            if (code is Start)
            {
                code.next = null;
                continue;
            }

            if (code.GetComponent<CodeBlockShelfInstance>() != null)
                continue;

            blocksToDestroy.Add(code);
        }

        foreach (var code in blocksToDestroy)
        {
            if (code == null)
                continue;

            connectionManager?.CleanupBlock(code);
            code.transform.SetParent(null, true);
            Destroy(code.gameObject);
        }
    }

    public void PlaceStartInWorkspace()
    {
        var startPrefab = GetStartPrefab();
        var start = FindObjectOfType<Start>();

        if (start == null && startPrefab != null)
            start = Instantiate(startPrefab).GetComponent<Start>();

        if (start == null)
            return;

        var shelf = start.GetComponent<CodeBlockShelfInstance>();
        if (shelf != null && shelf.sourceSlot != null)
            shelf.sourceSlot.ReleaseShelfBlock(start.gameObject, false);

        start.transform.SetParent(null, true);
        start.transform.SetPositionAndRotation(startDropPosition, Quaternion.Euler(startDropEulerAngles));
        if (startPrefab != null)
            start.transform.localScale = startPrefab.transform.localScale;

        var poolItem = start.GetComponent<CodeBlockPoolItem>();
        if (poolItem == null)
            poolItem = start.gameObject.AddComponent<CodeBlockPoolItem>();
        poolItem.sourcePrefab = startPrefab;

        var rb = start.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        ConnectionManager.Instance?.CleanupBlock(start);
    }

    public bool IsUniqueStartPrefab(GameObject prefab)
    {
        prefab = BlockIdentity.AsGameObject(prefab);
        return prefab != null && prefab.GetComponent<Start>() != null;
    }

    private GameObject GetStartPrefab()
    {
        if (catalog == null)
            return null;

        for (int i = 0; i < catalog.EntryCount; i++)
        {
            var entry = catalog.GetEntry(i);
            var prefab = catalog.GetPrefab(entry);
            if (IsUniqueStartPrefab(prefab))
                return prefab;
        }

        return null;
    }

    private void RemoveLooseTurnBlocks()
    {
        foreach (var code in FindObjectsOfType<Code>())
        {
            if (IsUnderBoard(code.transform))
                continue;

            if (!(code is TurnLeftCode) && !(code is TurnRightCode))
                continue;

            startDropPosition = code.transform.position;
            startDropEulerAngles = code.transform.eulerAngles;
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

    private void CaptureSceneBlockPoses()
    {
        sceneBlockPoses.Clear();

        foreach (var code in CollectBoardCodeBlocks())
        {
            var t = code.transform;
            sceneBlockPoses.Add(new SceneBlockPose
            {
                code = code,
                parent = t.parent,
                localPosition = t.localPosition,
                localRotation = t.localRotation,
                localScale = t.localScale
            });
        }
    }

    private void RestoreSceneBlockPoses()
    {
        foreach (var pose in sceneBlockPoses)
        {
            if (pose.code == null)
                continue;

            if (pose.code.GetComponent<CodeBlockShelfInstance>() == null)
                continue;

            var t = pose.code.transform;
            if (pose.parent != null && t.parent != pose.parent)
                t.SetParent(pose.parent, false);

            t.localPosition = pose.localPosition;
            t.localRotation = pose.localRotation;
            t.localScale = pose.localScale;
        }

        foreach (var slot in slots)
            slot.RefreshShelfPose();
    }

    private void DestroyGeneratedBoardVisuals()
    {
        string[] generated = { "BoardVisual", "BoardSurface", "FrameTop", "FrameBottom", "FrameLeft", "FrameRight" };
        foreach (var name in generated)
        {
            var child = transform.Find(name);
            if (child != null)
                Destroy(child.gameObject);
        }
    }

    private void BuildSlots()
    {
        slots.Clear();

        if (catalog == null || catalog.EntryCount == 0)
            return;

        EnsureSlotsParent();

        var sceneBlocks = CollectBoardCodeBlocks();
        if (sceneBlocks.Count > 0)
        {
            TryBuildSlotsFromSceneBlocks(sceneBlocks);
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

        Vector3 parentScale = transform.lossyScale;
        slotsParent.localScale = new Vector3(
            InverseScale(parentScale.x),
            InverseScale(parentScale.y),
            InverseScale(parentScale.z));
    }

    private static float InverseScale(float value)
    {
        return Mathf.Abs(value) < 0.0001f ? 1f : 1f / value;
    }

    private bool TryBuildSlotsFromSceneBlocks(List<Code> sceneBlocks)
    {
        if (sceneBlocks == null || sceneBlocks.Count == 0)
            return false;

        int slotIndex = 0;
        foreach (var code in sceneBlocks)
        {
            if (!catalog.TryGetEntryForGameObject(code.gameObject, out var entry))
            {
                Debug.LogWarning($"[CodeBlockBoard] Could not match scene block '{code.name}' to CodeBlockCatalog.", code);
                PinUnmatchedSceneBlock(code.gameObject);
                continue;
            }

            var prefab = catalog.GetPrefab(entry);
            if (prefab == null)
            {
                Debug.LogWarning($"[CodeBlockBoard] Catalog entry '{entry.displayName}' has no GameObject prefab.", code);
                PinUnmatchedSceneBlock(code.gameObject);
                continue;
            }

            var slotObject = new GameObject($"Slot_{entry.displayName}_{slotIndex}");
            slotObject.transform.SetParent(slotsParent, true);
            slotObject.transform.SetPositionAndRotation(code.transform.position, code.transform.rotation);
            slotObject.transform.localScale = Vector3.one;

            var slot = slotObject.AddComponent<CodeBlockSlot>();
            slot.blockPrefab = prefab;
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

    private static void PinUnmatchedSceneBlock(GameObject block)
    {
        if (block == null)
            return;

        var rb = block.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (block.GetComponent<CodeBlockShelfInstance>() == null)
            block.AddComponent<CodeBlockShelfInstance>();
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
            if (entry == null)
                continue;

            var prefab = catalog.GetPrefab(entry);
            if (prefab == null)
                continue;

            if (!IsUniqueStartPrefab(prefab))
                continue;

            counts.TryGetValue(prefab, out int sceneCount);
            if (sceneCount != 1)
            {
                Debug.LogWarning(
                    $"[CodeBlockBoard] Start should have exactly 1 block on the board, found {sceneCount}.");
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

            CreateSlotAt(slotAnchors[i], catalog.GetPrefab(entry), entry.displayName);
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

            CreateSlotAt(slotObject.transform, catalog.GetPrefab(entry), entry.displayName);
        }
    }

    private List<CodeBlockEntry> BuildExpandedSlotEntries()
    {
        var expanded = new List<CodeBlockEntry>();

        for (int i = 0; i < catalog.EntryCount; i++)
        {
            var entry = catalog.GetEntry(i);
            if (entry == null || catalog.GetPrefab(entry) == null)
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
            var prefab = BlockIdentity.AsGameObject(slot.blockPrefab);
            if (!slot.IsEmpty || prefab == null)
                continue;

            var block = Instantiate(prefab, slot.transform.position, slot.transform.rotation);
            block.transform.localScale = prefab.transform.localScale;
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
