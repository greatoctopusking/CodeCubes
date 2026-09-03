using System.Collections;
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
    private bool available = true;

    public bool IsEmpty => shelfBlock == null;
    public bool IsAvailable => available;

    public void RegisterPlacedBlock(GameObject block)
    {
        if (block == null)
            return;

        shelfBlock = block;

        var poolItem = block.GetComponent<CodeBlockPoolItem>();
        if (poolItem == null)
            poolItem = block.AddComponent<CodeBlockPoolItem>();

        poolItem.sourcePrefab = blockPrefab;

        // Capture local scale after parenting so later returns restore the on-shelf size
        // instead of compounding the board's non-uniform world scale.
        block.transform.SetParent(transform, true);
        shelfLocalScale = block.transform.localScale;
        ApplyShelfState(block);
        BindGrabListener(block);
        ApplyAvailabilityVisual();
    }

    public bool PlaceBlock(GameObject block)
    {
        if (!IsEmpty || block == null || blockPrefab == null)
            return false;

        var poolItem = block.GetComponent<CodeBlockPoolItem>();
        if (poolItem == null || poolItem.sourcePrefab != blockPrefab)
            return false;

        shelfBlock = block;

        block.transform.SetParent(transform, true);
        block.transform.SetPositionAndRotation(transform.position, transform.rotation);
        block.transform.localScale = shelfLocalScale;

        ApplyShelfState(block);
        BindGrabListener(block);
        ApplyAvailabilityVisual();
        return true;
    }

    public void SetShelfAvailable(bool value)
    {
        available = value;
        ApplyAvailabilityVisual();
    }

    private void ApplyAvailabilityVisual()
    {
        if (shelfBlock == null)
            return;

        if (available)
        {
            if (!shelfBlock.activeSelf)
                shelfBlock.SetActive(true);
            SetGrabEnabled(shelfBlock, true);
        }
        else
        {
            SetGrabEnabled(shelfBlock, false);
            if (shelfBlock.activeSelf)
                shelfBlock.SetActive(false);
        }
    }

    private static void SetGrabEnabled(GameObject block, bool enabled)
    {
        var grab = block.GetComponent<XRGrabInteractable>();
        if (grab != null)
            grab.enabled = enabled;
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

        // Clear shelf bookkeeping immediately; defer SetParent so XR Instantaneous
        // grab can parent to the interactor first (PLAN H2).
        ReleaseShelfBlock(grabbedObject, deferWorldDetach: true);
    }

    public void ReleaseShelfBlock(GameObject block, bool deferWorldDetach = false)
    {
        if (block == null || block != shelfBlock)
            return;

        var grab = block.GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnShelfBlockGrabbed);
            grab.enabled = true;
        }

        var shelfMarker = block.GetComponent<CodeBlockShelfInstance>();
        if (shelfMarker != null)
        {
            if (Application.isPlaying)
                Destroy(shelfMarker);
            else
                DestroyImmediate(shelfMarker);
        }

        var rb = block.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        // Mark empty immediately so grab does not restock.
        shelfBlock = null;

        if (Application.isPlaying && deferWorldDetach)
        {
            StartCoroutine(DetachFromSlotNextFrame(block));
            return;
        }

        block.transform.SetParent(null, true);
    }

    private IEnumerator DetachFromSlotNextFrame(GameObject block)
    {
        yield return null;

        if (block == null)
            yield break;

        // Only detach if XR left the block parented to this slot.
        if (block.transform.parent == transform)
            block.transform.SetParent(null, true);
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
