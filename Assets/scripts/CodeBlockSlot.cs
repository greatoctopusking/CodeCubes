using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class CodeBlockSlot : MonoBehaviour
{
    public GameObject blockPrefab;
    public string displayName;

    [HideInInspector] public CodeBlockBoard board;

    private GameObject shelfBlock;
    private Vector3 shelfLocalScale = Vector3.one;

    public bool IsEmpty => shelfBlock == null;

    public void RegisterPlacedBlock(GameObject block)
    {
        if (block == null)
            return;

        shelfBlock = block;
        shelfLocalScale = block.transform.localScale;

        var poolItem = block.GetComponent<CodeBlockPoolItem>();
        if (poolItem == null)
            poolItem = block.AddComponent<CodeBlockPoolItem>();

        poolItem.sourcePrefab = blockPrefab;

        block.transform.SetParent(transform, true);
        ApplyShelfState(block);
        BindGrabListener(block);
    }

    public bool PlaceBlock(GameObject block)
    {
        if (!IsEmpty || block == null || blockPrefab == null)
            return false;

        var poolItem = block.GetComponent<CodeBlockPoolItem>();
        if (poolItem == null || poolItem.sourcePrefab != blockPrefab)
            return false;

        shelfBlock = block;
        shelfLocalScale = block.transform.localScale;

        block.transform.SetParent(transform, true);
        block.transform.SetPositionAndRotation(transform.position, transform.rotation);
        block.transform.localScale = shelfLocalScale;

        ApplyShelfState(block);
        BindGrabListener(block);
        return true;
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
        }

        var shelfMarker = block.GetComponent<CodeBlockShelfInstance>();
        if (shelfMarker == null)
            shelfMarker = block.AddComponent<CodeBlockShelfInstance>();

        shelfMarker.sourceSlot = this;
        shelfMarker.sourcePrefab = blockPrefab;
    }

    private void BindGrabListener(GameObject block)
    {
        var grab = block.GetComponent<XRGrabInteractable>();
        if (grab == null)
            return;

        grab.selectEntered.RemoveListener(OnShelfBlockGrabbed);
        grab.selectEntered.AddListener(OnShelfBlockGrabbed);
    }

    private void OnShelfBlockGrabbed(SelectEnterEventArgs args)
    {
        var grabbedObject = args.interactableObject.transform.gameObject;
        if (grabbedObject != shelfBlock)
            return;

        ReleaseShelfBlock(grabbedObject);
    }

    public void ReleaseShelfBlock(GameObject block)
    {
        if (block == null || block != shelfBlock)
            return;

        var grab = block.GetComponent<XRGrabInteractable>();
        if (grab != null)
            grab.selectEntered.RemoveListener(OnShelfBlockGrabbed);

        var shelfMarker = block.GetComponent<CodeBlockShelfInstance>();
        if (shelfMarker != null)
            Destroy(shelfMarker);

        var rb = block.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        // Detach from board hierarchy so workspace / ClearWorkspace treat it as taken.
        block.transform.SetParent(null, true);

        shelfBlock = null;
    }

    private void OnDestroy()
    {
        if (shelfBlock == null)
            return;

        var grab = shelfBlock.GetComponent<XRGrabInteractable>();
        if (grab != null)
            grab.selectEntered.RemoveListener(OnShelfBlockGrabbed);
    }
}
