using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class CodeBlockSlot : MonoBehaviour
{
    public GameObject blockPrefab;
    public string displayName;

    [HideInInspector] public CodeBlockBoard board;

    private GameObject shelfBlock;
    private Transform shelfParent;
    private Vector3 shelfLocalPosition;
    private Quaternion shelfLocalRotation;
    private Vector3 shelfLocalScale = Vector3.one;

    public bool IsEmpty => shelfBlock == null;

    public Vector3 ShelfWorldPosition => shelfBlock != null ? shelfBlock.transform.position : transform.position;

    public void MoveShelfBlock(Vector3 worldPosition)
    {
        if (shelfBlock == null)
            return;

        shelfBlock.transform.position = worldPosition;
        transform.SetPositionAndRotation(shelfBlock.transform.position, shelfBlock.transform.rotation);
        CaptureShelfPose(shelfBlock);
    }

    public void RefreshShelfPose()
    {
        if (shelfBlock == null)
            return;

        transform.SetPositionAndRotation(shelfBlock.transform.position, shelfBlock.transform.rotation);
        CaptureShelfPose(shelfBlock);
        SyncRigidbodyToTransform(shelfBlock);
    }

    private bool RefillOnTake
    {
        get
        {
            var prefab = BlockIdentity.AsGameObject(blockPrefab);
            return prefab == null || prefab.GetComponent<Start>() == null;
        }
    }

    public void RegisterPlacedBlock(GameObject block)
    {
        if (block == null)
            return;

        shelfBlock = block;
        CaptureShelfPose(block);

        var poolItem = block.GetComponent<CodeBlockPoolItem>();
        if (poolItem == null)
            poolItem = block.AddComponent<CodeBlockPoolItem>();

        poolItem.sourcePrefab = BlockIdentity.AsGameObject(blockPrefab);

        ApplyShelfState(block);
        BindGrabListener(block);
    }

    public bool PlaceBlock(GameObject block)
    {
        if (!IsEmpty || block == null)
            return false;

        var prefab = BlockIdentity.AsGameObject(blockPrefab);
        var poolItem = block.GetComponent<CodeBlockPoolItem>();
        if (prefab == null || poolItem == null || poolItem.sourcePrefab != prefab)
            return false;

        shelfBlock = block;
        ApplyShelfState(block);
        RestoreShelfPose(block);
        BindGrabListener(block);
        return true;
    }

    public void ReleaseShelfBlock(GameObject block, bool refill = true)
    {
        if (block == null || block != shelfBlock)
            return;

        var grab = block.GetComponent<XRGrabInteractable>();
        if (grab != null)
            grab.selectEntered.RemoveListener(OnShelfBlockGrabbed);

        var shelfMarker = block.GetComponent<CodeBlockShelfInstance>();
        if (shelfMarker != null)
            Destroy(shelfMarker);

        DetachFromBoardIfNeeded(block);
        block.transform.SetParent(null, true);
        ApplyWorkspaceScale(block);

        shelfBlock = null;

        if (refill && RefillOnTake)
            SpawnReplacement();
    }

    private void CaptureShelfPose(GameObject block)
    {
        shelfParent = block.transform.parent;
        shelfLocalPosition = block.transform.localPosition;
        shelfLocalRotation = block.transform.localRotation;
        shelfLocalScale = block.transform.localScale;
    }

    private void RestoreShelfPose(GameObject block)
    {
        if (shelfParent != null)
            block.transform.SetParent(shelfParent, false);

        block.transform.localPosition = shelfLocalPosition;
        block.transform.localRotation = shelfLocalRotation;
        block.transform.localScale = shelfLocalScale;
    }

    private void SpawnReplacement()
    {
        var prefab = BlockIdentity.AsGameObject(blockPrefab);
        if (prefab == null)
            return;

        var replacement = Instantiate(prefab);
        replacement.name = prefab.name;
        RestoreShelfPose(replacement);
        RegisterPlacedBlock(replacement);
    }

    private void ApplyWorkspaceScale(GameObject block)
    {
        var prefab = BlockIdentity.AsGameObject(blockPrefab);
        if (prefab != null)
            block.transform.localScale = prefab.transform.localScale;
    }

    private void ApplyShelfState(GameObject block)
    {
        var rb = block.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
            SyncRigidbodyToTransform(block);
        }

        var shelfMarker = block.GetComponent<CodeBlockShelfInstance>();
        if (shelfMarker == null)
            shelfMarker = block.AddComponent<CodeBlockShelfInstance>();

        shelfMarker.sourceSlot = this;
        shelfMarker.sourcePrefab = BlockIdentity.AsGameObject(blockPrefab);
    }

    private void BindGrabListener(GameObject block)
    {
        var grab = block.GetComponent<XRGrabInteractable>();
        if (grab == null)
            return;

        grab.selectEntered.RemoveListener(OnShelfBlockGrabbed);
        grab.selectEntered.AddListener(OnShelfBlockGrabbed);
        grab.selectExited.RemoveListener(OnWorkspaceBlockReleased);
        grab.selectExited.AddListener(OnWorkspaceBlockReleased);
    }

    private void OnShelfBlockGrabbed(SelectEnterEventArgs args)
    {
        var grabbedObject = args.interactableObject.transform.gameObject;
        if (grabbedObject != shelfBlock)
            return;

        ReleaseShelfBlock(grabbedObject);
    }

    private void OnWorkspaceBlockReleased(SelectExitEventArgs args)
    {
        var block = args.interactableObject.transform.gameObject;
        if (block == null || block.GetComponent<CodeBlockShelfInstance>() != null)
            return;

        var grab = block.GetComponent<XRGrabInteractable>();
        if (grab != null && grab.isSelected)
            return;

        var rb = block.GetComponent<Rigidbody>();
        if (rb == null)
            return;

        rb.isKinematic = false;
        rb.useGravity = true;
    }

    private static void SyncRigidbodyToTransform(GameObject block)
    {
        var rb = block.GetComponent<Rigidbody>();
        if (rb == null)
            return;

        rb.position = block.transform.position;
        rb.rotation = block.transform.rotation;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void DetachFromBoardIfNeeded(GameObject block)
    {
        var parent = block.transform.parent;
        if (parent == null)
            return;

        bool underThisSlot = parent == transform || block.transform.IsChildOf(transform);
        bool underBoard = board != null && block.transform.IsChildOf(board.transform);
        if (!underThisSlot && !underBoard)
            return;

        block.transform.SetParent(null, true);
    }

    private void OnDestroy()
    {
        if (shelfBlock == null)
            return;

        var grab = shelfBlock.GetComponent<XRGrabInteractable>();
        if (grab == null)
            return;

        grab.selectEntered.RemoveListener(OnShelfBlockGrabbed);
        grab.selectExited.RemoveListener(OnWorkspaceBlockReleased);
    }
}
