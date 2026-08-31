using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Collider))]
public class TrashCan : MonoBehaviour
{
    [Header("Visual")]
    [Tooltip("Optional 3D model prefab. Skipped when a visual child already exists in the scene.")]
    public GameObject visualPrefab;
    public bool respectExistingVisual = true;
    public Vector3 visualLocalPosition = Vector3.zero;
    public Vector3 visualLocalEulerAngles = Vector3.zero;
    public Vector3 visualLocalScale = Vector3.one;
    public Color bodyColor = new Color(0.25f, 0.28f, 0.3f, 1f);
    public Color rimColor = new Color(0.45f, 0.48f, 0.5f, 1f);

    private CodeManager codeManager;
    private CodeBlockBoard board;

    private readonly HashSet<Code> blocksInside = new HashSet<Code>();
    private readonly Dictionary<Code, XRGrabInteractable> grabSubscriptions =
        new Dictionary<Code, XRGrabInteractable>();

    private void Awake()
    {
        codeManager = FindObjectOfType<CodeManager>();
        board = CodeBlockBoard.Instance ?? FindObjectOfType<CodeBlockBoard>();
        EnsureTriggerCollider();
        EnsureVisual();
    }

    private void OnDestroy()
    {
        foreach (var pair in grabSubscriptions)
        {
            if (pair.Value != null)
                pair.Value.selectExited.RemoveListener(OnBlockSelectExited);
        }

        grabSubscriptions.Clear();
        blocksInside.Clear();
    }

    private void EnsureTriggerCollider()
    {
        var collider = GetComponent<Collider>();
        collider.isTrigger = true;
    }

    private void EnsureVisual()
    {
        if (transform.Find("TrashVisual") != null || transform.Find("Body") != null)
            return;

        if (respectExistingVisual && HasExistingVisual())
        {
            StripColliders(gameObject);
            return;
        }

        if (visualPrefab != null)
        {
            var visual = Instantiate(visualPrefab, transform);
            visual.name = "TrashVisual";
            visual.transform.localPosition = visualLocalPosition;
            visual.transform.localEulerAngles = visualLocalEulerAngles;
            visual.transform.localScale = visualLocalScale;
            StripColliders(visual);
            return;
        }

        CreateProceduralVisual();
    }

    private bool HasExistingVisual()
    {
        if (GetComponent<Renderer>() != null)
            return true;

        foreach (Transform child in transform)
        {
            if (child.GetComponentInChildren<Renderer>() != null)
                return true;
        }

        return false;
    }

    private void CreateProceduralVisual()
    {
        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "Body";
        body.transform.SetParent(transform, false);
        body.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        body.transform.localScale = new Vector3(0.35f, 0.25f, 0.35f);

        var bodyCollider = body.GetComponent<Collider>();
        if (bodyCollider != null)
            Destroy(bodyCollider);

        ApplyColor(body, bodyColor);

        var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rim.name = "Rim";
        rim.transform.SetParent(transform, false);
        rim.transform.localPosition = new Vector3(0f, 0.48f, 0f);
        rim.transform.localScale = new Vector3(0.4f, 0.03f, 0.4f);

        var rimCollider = rim.GetComponent<Collider>();
        if (rimCollider != null)
            Destroy(rimCollider);

        ApplyColor(rim, rimColor);
    }

    private static void StripColliders(GameObject root)
    {
        foreach (var collider in root.GetComponentsInChildren<Collider>())
        {
            if (collider.gameObject == root && collider is BoxCollider box && box.isTrigger)
                continue;

            Destroy(collider);
        }
    }

    private static void ApplyColor(GameObject target, Color color)
    {
        var renderer = target.GetComponent<Renderer>();
        if (renderer == null)
            return;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        renderer.material = new Material(shader) { color = color };
    }

    private void OnTriggerEnter(Collider other)
    {
        var code = ResolveCode(other);
        if (!IsReturnCandidate(code))
            return;

        blocksInside.Add(code);
        SubscribeGrab(code);

        // Already released (thrown into can) → return immediately.
        if (!IsGrabbed(code))
            TryReturnBlock(code);
    }

    private void OnTriggerExit(Collider other)
    {
        var code = ResolveCode(other);
        if (code == null)
            return;

        blocksInside.Remove(code);
        UnsubscribeGrab(code);
    }

    private void OnBlockSelectExited(SelectExitEventArgs args)
    {
        var code = args.interactableObject.transform.GetComponentInParent<Code>();
        if (code == null || !blocksInside.Contains(code))
            return;

        TryReturnBlock(code);
    }

    private void TryReturnBlock(Code code)
    {
        if (!IsReturnCandidate(code))
            return;

        if (IsGrabbed(code))
            return;

        if (board == null)
            board = CodeBlockBoard.Instance ?? FindObjectOfType<CodeBlockBoard>();

        if (board == null)
        {
            Debug.LogWarning("[TrashCan] CodeBlockBoard not found. Cannot return block.");
            return;
        }

        blocksInside.Remove(code);
        UnsubscribeGrab(code);

        ConnectionManager.Instance?.CleanupBlock(code);

        if (board.ReturnBlock(code))
            return;

        Debug.LogWarning($"[TrashCan] No empty shelf slot available for '{code.name}'. Block was not destroyed.");
    }

    private bool IsReturnCandidate(Code code)
    {
        if (code == null)
            return false;

        if (codeManager != null && codeManager.IsExecuting)
            return false;

        if (code.GetComponent<CodeBlockShelfInstance>() != null)
            return false;

        return true;
    }

    private static Code ResolveCode(Collider other)
    {
        return other != null ? other.GetComponentInParent<Code>() : null;
    }

    private static bool IsGrabbed(Code code)
    {
        if (code == null)
            return false;

        var grab = code.GetComponent<XRGrabInteractable>();
        return grab != null && grab.isSelected;
    }

    private void SubscribeGrab(Code code)
    {
        if (code == null || grabSubscriptions.ContainsKey(code))
            return;

        var grab = code.GetComponent<XRGrabInteractable>();
        if (grab == null)
            return;

        grab.selectExited.AddListener(OnBlockSelectExited);
        grabSubscriptions[code] = grab;
    }

    private void UnsubscribeGrab(Code code)
    {
        if (code == null || !grabSubscriptions.TryGetValue(code, out var grab))
            return;

        if (grab != null)
            grab.selectExited.RemoveListener(OnBlockSelectExited);

        grabSubscriptions.Remove(code);
    }
}
