using UnityEngine;
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

    private void Awake()
    {
        codeManager = FindObjectOfType<CodeManager>();
        board = FindObjectOfType<CodeBlockBoard>();
        EnsureTriggerCollider();
        EnsureVisual();
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

        if (respectExistingVisual && GetComponentInChildren<Renderer>(true) != null)
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
        TryReturnBlock(other);
    }

    private void TryReturnBlock(Collider other)
    {
        if (codeManager != null && codeManager.IsExecuting)
            return;

        var code = other.GetComponentInParent<Code>();
        if (code == null)
            return;

        if (code.GetComponent<CodeBlockShelfInstance>() != null)
            return;

        if (board == null)
            board = CodeBlockBoard.Instance ?? FindObjectOfType<CodeBlockBoard>();

        if (board == null)
        {
            Debug.LogWarning("[TrashCan] CodeBlockBoard not found. Cannot return block.");
            return;
        }

        ReleaseGrab(code.gameObject);
        ConnectionManager.Instance?.CleanupBlock(code);

        if (code is Start)
        {
            board.PlaceStartInWorkspace();
            return;
        }

        Destroy(code.gameObject);
    }

    private static void ReleaseGrab(GameObject block)
    {
        var grab = block.GetComponent<XRGrabInteractable>();
        if (grab == null || !grab.isSelected || grab.interactionManager == null)
            return;

        var interactor = grab.firstInteractorSelecting;
        if (interactor != null)
            grab.interactionManager.SelectExit(interactor, grab);
    }
}
