using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

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

    [Header("Start Block")]
    [Tooltip("Optional. When set, the unique Start block is placed here at the start of each level.")]
    public Transform startBlockSpawnPoint;

    [Tooltip("Used when Start Block Spawn Point is empty. Place the unique Start on the floor near the board.")]
    public Vector3 startBlockGroundPosition = new Vector3(1.95f, 0.4f, 6.9f);

    private readonly List<CodeBlockSlot> slots = new List<CodeBlockSlot>();
    private readonly Dictionary<string, WallStackTemplate> wallStacks = new Dictionary<string, WallStackTemplate>();
    private Transform slotsParent;
    private Vector3 startWorkspaceScale = Vector3.one;
    private bool hasStartWorkspaceScale;

    private struct WallStackTemplate
    {
        public CodeBlockEntry entry;
        public Transform wallParent;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public GameObject templateBlock;
    }

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
            if (catalog == null || !catalog.TryGetEntryForGameObject(code.gameObject, out var entry) || entry.prefab == null)
                return false;

            poolItem = code.gameObject.AddComponent<CodeBlockPoolItem>();
            poolItem.sourcePrefab = entry.prefab;
        }

        foreach (var slot in slots)
        {
            if (slot == null || !slot.IsEmpty || !slot.IsAvailable || slot.blockPrefab == null)
                continue;

            bool sameType = slot.blockPrefab == poolItem.sourcePrefab ||
                            BlockIdentity.Matches(code.gameObject, slot.blockPrefab);
            if (!sameType)
                continue;

            poolItem.sourcePrefab = slot.blockPrefab;
            return slot.PlaceBlock(code.gameObject);
        }

        return false;
    }

    public void ApplyAvailableBlocks(string[] allowedNames)
    {
        var enabledCountByName = new Dictionary<string, int>();

        foreach (var slot in slots)
        {
            if (slot == null)
                continue;

            bool typeAllowed = LevelBlockHints.IsAllowed(slot.displayName, allowedNames);
            enabledCountByName.TryGetValue(slot.displayName ?? string.Empty, out int used);
            int cap = LevelBlockHints.GetCopyCap(slot.displayName, allowedNames);
            bool enable = typeAllowed && used < cap;

            slot.SetShelfAvailable(enable);
            if (enable)
                enabledCountByName[slot.displayName ?? string.Empty] = used + 1;
        }
    }

    public void ClearWorkspace()
    {
        var connectionManager = ConnectionManager.Instance;
        var codes = FindObjectsOfType<Code>();
        var blocksToReturn = new List<Code>();

        foreach (var code in codes)
        {
            if (code == null)
                continue;

            if (code.GetComponent<CodeBlockShelfInstance>() != null)
                continue;

            blocksToReturn.Add(code);
        }

        foreach (var code in blocksToReturn)
        {
            if (code == null)
                continue;

            connectionManager?.CleanupBlock(code);

            if (ReturnBlock(code))
                continue;

            Debug.LogWarning($"[CodeBlockBoard] ClearWorkspace could not return '{code.name}'. Destroying as last resort.");
            DestroyBlock(code.gameObject);
        }
    }

    /// <summary>
    /// Ensures exactly one Start exists and places it on the ground for the current level.
    /// Taking it off the shelf does not restock the wall.
    /// </summary>
    public void PlaceUniqueStartOnGround()
    {
        var starts = FindObjectsOfType<Start>(true);
        Start keep = FindPreferredStart(starts);

        foreach (var start in starts)
        {
            if (start == null || start == keep)
                continue;

            ReleaseFromShelf(start);
            DestroyBlock(start.gameObject);
        }

        if (keep == null)
            keep = SpawnStartFromCatalog();

        if (keep == null)
        {
            Debug.LogWarning("[CodeBlockBoard] No Start block available to place on the ground.");
            return;
        }

        ReleaseFromShelf(keep);
        ConnectionManager.Instance?.CleanupBlock(keep);

        keep.transform.SetParent(null, true);
        keep.transform.position = ResolveStartGroundPosition();

        if (!hasStartWorkspaceScale)
        {
            startWorkspaceScale = keep.transform.localScale;
            hasStartWorkspaceScale = true;
        }
        else
        {
            keep.transform.localScale = startWorkspaceScale;
        }

        var rb = keep.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }

    private void InitializePool()
    {
        if (catalog == null)
            return;

        ValidateCatalogCounts();
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
        wallStacks.Clear();

        if (catalog == null || catalog.EntryCount == 0)
            return;

        EnsureSlotsParent();

        // Prefer scene-placed board blocks. Never spawn Cube fallbacks when any exist.
        var sceneBlocks = CollectBoardCodeBlocks();
        if (sceneBlocks.Count > 0)
        {
            TryBuildSlotsFromSceneBlocks(sceneBlocks);
            PruneInactiveStartDuplicates();
            int adoptedFromScene = slots.Count;
            FillMissingCatalogSlots();
            ValidateCatalogCounts();

            if (slots.Count == 0)
            {
                Debug.LogError(
                    $"[CodeBlockBoard] Found {sceneBlocks.Count} scene block(s) under the board but none matched CodeBlockCatalog. " +
                    "Check BlockIdentity / prefab names. Runtime Cube spawn was skipped to avoid duplicates.");
            }
            else if (adoptedFromScene != sceneBlocks.Count)
            {
                Debug.LogWarning(
                    $"[CodeBlockBoard] Matched {adoptedFromScene}/{sceneBlocks.Count} scene block(s) to Catalog. " +
                    "Unmatched blocks stay on the board but are outside the pool.");
            }

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

    private bool TryBuildSlotsFromSceneBlocks(List<Code> sceneBlocks)
    {
        if (sceneBlocks == null || sceneBlocks.Count == 0)
            return false;

        sceneBlocks.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        int slotIndex = 0;
        var countsByPrefab = new Dictionary<GameObject, int>();

        foreach (var code in sceneBlocks)
        {
            if (code == null || !code.gameObject.activeInHierarchy)
                continue;

            // Drop stale pool bindings so rematch uses live Code type / name.
            var stalePool = code.GetComponent<CodeBlockPoolItem>();
            if (stalePool != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(stalePool);
                else
                    UnityEngine.Object.DestroyImmediate(stalePool);
            }

            if (!catalog.TryGetEntryForGameObject(code.gameObject, out var entry) || entry.prefab == null)
            {
                Debug.LogWarning($"[CodeBlockBoard] Could not match scene block '{code.name}' to CodeBlockCatalog.", code);
                continue;
            }

            countsByPrefab.TryGetValue(entry.prefab, out int existing);
            int max = Mathf.Max(1, entry.maxCount);
            if (existing >= max)
            {
                DestroyBlock(code.gameObject);
                continue;
            }

            countsByPrefab[entry.prefab] = existing + 1;

            RememberWallStack(entry, code);

            var slotObject = new GameObject($"Slot_{entry.displayName}_{slotIndex}");
            slotObject.transform.SetParent(code.transform.parent, false);
            slotObject.transform.localPosition = code.transform.localPosition;
            slotObject.transform.localRotation = code.transform.localRotation;
            slotObject.transform.localScale = Vector3.one;

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

            if (!code.gameObject.activeInHierarchy)
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

    private void RememberWallStack(CodeBlockEntry entry, Code code)
    {
        if (entry == null || code == null || string.IsNullOrEmpty(entry.displayName))
            return;

        if (wallStacks.ContainsKey(entry.displayName))
            return;

        wallStacks[entry.displayName] = new WallStackTemplate
        {
            entry = entry,
            wallParent = code.transform.parent,
            localPosition = code.transform.localPosition,
            localRotation = code.transform.localRotation,
            localScale = code.transform.localScale,
            templateBlock = code.gameObject
        };
    }

    private void FillMissingCatalogSlots()
    {
        if (catalog == null)
            return;

        for (int i = 0; i < catalog.EntryCount; i++)
        {
            var entry = catalog.GetEntry(i);
            if (entry == null || entry.prefab == null)
                continue;

            int max = Mathf.Max(1, entry.maxCount);
            int existingCount = 0;
            foreach (var slot in slots)
            {
                if (slot != null && slot.blockPrefab == entry.prefab)
                    existingCount++;
            }

            if (existingCount >= max)
                continue;

            if (!wallStacks.TryGetValue(entry.displayName, out var stack) ||
                stack.wallParent == null ||
                stack.templateBlock == null)
            {
                Debug.LogWarning(
                    $"[CodeBlockBoard] '{entry.displayName}' needs {max} copies but no wall stack template exists.");
                continue;
            }

            for (int copy = existingCount; copy < max; copy++)
                SpawnWallStackCopy(stack, copy);
        }
    }

    private void SpawnWallStackCopy(WallStackTemplate stack, int copyIndex)
    {
        var entry = stack.entry;
        var slotObject = new GameObject($"Slot_{entry.displayName}_{copyIndex}");
        slotObject.transform.SetParent(stack.wallParent, false);
        slotObject.transform.localPosition = stack.localPosition;
        slotObject.transform.localRotation = stack.localRotation;
        slotObject.transform.localScale = Vector3.one;

        var slot = slotObject.AddComponent<CodeBlockSlot>();
        slot.blockPrefab = entry.prefab;
        slot.displayName = entry.displayName;
        slot.board = this;
        slots.Add(slot);

        var clone = Instantiate(stack.templateBlock);
        clone.name = stack.templateBlock.name;
        StripCloneRuntimeState(clone);
        clone.transform.SetParent(stack.wallParent, false);
        clone.transform.localPosition = stack.localPosition;
        clone.transform.localRotation = stack.localRotation;
        clone.transform.localScale = stack.localScale;
        slot.RegisterPlacedBlock(clone);
    }

    private static void StripCloneRuntimeState(GameObject clone)
    {
        if (clone == null)
            return;

        var grab = clone.GetComponent<XRGrabInteractable>();
        if (grab != null)
            grab.selectEntered.RemoveAllListeners();

        var shelf = clone.GetComponent<CodeBlockShelfInstance>();
        if (shelf != null)
            DestroyImmediate(shelf);

        var pool = clone.GetComponent<CodeBlockPoolItem>();
        if (pool != null)
            DestroyImmediate(pool);
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

    private static Start FindPreferredStart(Start[] starts)
    {
        if (starts == null)
            return null;

        Start workspace = null;
        Start shelf = null;
        Start any = null;

        foreach (var start in starts)
        {
            if (start == null)
                continue;

            if (any == null)
                any = start;

            if (!start.gameObject.activeInHierarchy)
                continue;

            if (start.GetComponent<CodeBlockShelfInstance>() == null)
            {
                if (workspace == null)
                    workspace = start;
            }
            else if (shelf == null)
            {
                shelf = start;
            }
        }

        if (workspace != null)
            return workspace;
        if (shelf != null)
            return shelf;
        return any;
    }

    private void PruneInactiveStartDuplicates()
    {
        foreach (var start in GetComponentsInChildren<Start>(true))
        {
            if (start == null || start.gameObject.activeInHierarchy)
                continue;

            DestroyBlock(start.gameObject);
        }
    }

    private static void ReleaseFromShelf(Start start)
    {
        if (start == null)
            return;

        var shelf = start.GetComponent<CodeBlockShelfInstance>();
        if (shelf == null || shelf.sourceSlot == null)
            return;

        shelf.sourceSlot.ReleaseShelfBlock(start.gameObject);
    }

    private Start SpawnStartFromCatalog()
    {
        if (catalog == null)
            return null;

        for (int i = 0; i < catalog.EntryCount; i++)
        {
            var entry = catalog.GetEntry(i);
            if (entry == null || entry.prefab == null)
                continue;

            if (!BlockIdentity.NamesMatch(entry.displayName, "Start") &&
                !BlockIdentity.NamesMatch(entry.prefab.name, "Start"))
                continue;

            var spawned = Instantiate(entry.prefab, ResolveStartGroundPosition(), Quaternion.identity);
            spawned.name = entry.displayName;

            var poolItem = spawned.GetComponent<CodeBlockPoolItem>();
            if (poolItem == null)
                poolItem = spawned.AddComponent<CodeBlockPoolItem>();
            poolItem.sourcePrefab = entry.prefab;

            return spawned.GetComponent<Start>();
        }

        return null;
    }

    private Vector3 ResolveStartGroundPosition()
    {
        if (startBlockSpawnPoint != null)
            return startBlockSpawnPoint.position;

        if (startBlockGroundPosition.sqrMagnitude > 0.0001f)
            return startBlockGroundPosition;

        return new Vector3(transform.position.x, 0.4f, transform.position.z) + transform.forward * 0.8f;
    }

    private static void DestroyBlock(GameObject block)
    {
        if (block == null)
            return;

        if (Application.isPlaying)
            Destroy(block);
        else
            DestroyImmediate(block);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 0.3f, 0.35f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(new Vector3(0f, 0f, 0.06f), new Vector3(1.9f, 1.2f, 0.08f));
    }
}
