using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class ConnectionManager : MonoBehaviour
{
    public static ConnectionManager Instance { get; private set; }

    [Header("Input")]
    public InputActionAsset inputActions;

    [Header("Visuals")]
    public Material previewLineMaterial;
    public Material connectionLineMaterial;
    public Material judgerLineMaterial;

    [Header("Settings")]
    public LayerMask blockLayerMask = 1 << 3;
    public float maxPreviewDistance = 20f;

    private InputAction rightActivateAction;
    private NearFarInteractor rightNearFarInteractor;
    private Transform fallbackRayOrigin;
    private CodeManager codeManager;
    private Code selectedBlock;

    private bool autoCreatedPreviewMaterial;
    private bool autoCreatedConnectionMaterial;
    private bool autoCreatedJudgerMaterial;

    private GameObject previewContainer;
    private LineRenderer previewLineRenderer;
    private GameObject previewArrowhead;

    private readonly List<ConnectionData> connections = new List<ConnectionData>();
    private GameObject connectionsContainer;
    private bool isOverUI;
    private UnityEngine.Events.UnityAction<UIHoverEventArgs> onUIHoverEntered;
    private UnityEngine.Events.UnityAction<UIHoverEventArgs> onUIHoverExited;

    private class ConnectionData
    {
        public Code from;
        public Code to;
        public LineRenderer line;
        public GameObject arrowhead;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        codeManager = FindObjectOfType<CodeManager>();

        if (inputActions == null)
        {
            Debug.LogError("[CM] inputActions is null");
            return;
        }

        var rightMap = inputActions.FindActionMap("XRI Right Interaction");
        if (rightMap == null)
        {
            Debug.LogError("[CM] Action map 'XRI Right Interaction' not found");
            return;
        }

        rightActivateAction = rightMap.FindAction("Activate");
        if (rightActivateAction == null)
        {
            Debug.LogError("[CM] 'Activate' not found in XRI Right Interaction");
            return;
        }

        rightActivateAction.Enable();
        rightActivateAction.performed += OnActivatePerformed;
        Debug.Log("[CM] Listening to right Activate");

        FindRayOrigin();

        if (previewLineMaterial == null)
        {
            previewLineMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { color = new Color(0.3f, 0.7f, 1f, 0.5f) };
            autoCreatedPreviewMaterial = true;
        }
        if (connectionLineMaterial == null)
        {
            connectionLineMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { color = new Color(1f, 0.85f, 0f, 0.6f) };
            autoCreatedConnectionMaterial = true;
        }
        if (judgerLineMaterial == null)
        {
            judgerLineMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { color = new Color(0.2f, 0.9f, 0.3f, 0.6f) };
            autoCreatedJudgerMaterial = true;
        }

        SetupXRIUI();

        connectionsContainer = new GameObject("ConnectionLines");
        connectionsContainer.transform.SetParent(transform);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (rightActivateAction != null)
        {
            rightActivateAction.performed -= OnActivatePerformed;
            rightActivateAction.Disable();
        }
        if (rightNearFarInteractor != null)
        {
            if (onUIHoverEntered != null) rightNearFarInteractor.uiHoverEntered.RemoveListener(onUIHoverEntered);
            if (onUIHoverExited != null) rightNearFarInteractor.uiHoverExited.RemoveListener(onUIHoverExited);
        }
        if (autoCreatedPreviewMaterial && previewLineMaterial != null) Destroy(previewLineMaterial);
        if (autoCreatedConnectionMaterial && connectionLineMaterial != null) Destroy(connectionLineMaterial);
        if (autoCreatedJudgerMaterial && judgerLineMaterial != null) Destroy(judgerLineMaterial);
    }

    private void FindRayOrigin()
    {
        var xrOrigin = FindObjectOfType<XROrigin>();
        if (xrOrigin != null)
        {
            var rightCtrl = FindDeepChild(xrOrigin.transform, "Right Controller");
            if (rightCtrl != null)
            {
                var nfi = rightCtrl.GetComponentInChildren<NearFarInteractor>(includeInactive: true);
                if (nfi != null)
                {
                    rightNearFarInteractor = nfi;
                    rightNearFarInteractor.enableUIInteraction = true;
                    onUIHoverEntered = _ => isOverUI = true;
                    onUIHoverExited = _ => isOverUI = false;
                    rightNearFarInteractor.uiHoverEntered.AddListener(onUIHoverEntered);
                    rightNearFarInteractor.uiHoverExited.AddListener(onUIHoverExited);

                    if (!nfi.gameObject.activeInHierarchy)
                    {
                        nfi.gameObject.SetActive(true);
                        Debug.Log("[CM] Activated NearFarInteractor GameObject");
                    }

                    var caster = nfi.farInteractionCaster as CurveInteractionCaster;
                    if (caster != null)
                    {
                        caster.raycastMask |= 1 << 3;
                        caster.castDistance = 25f;
                    }
                    else
                    {
                        Debug.LogWarning("[CM] farInteractionCaster is not CurveInteractionCaster");
                    }
                    return;
                }

                fallbackRayOrigin = rightCtrl;
                Debug.Log("[CM] Ray origin: Right Controller Transform");
                return;
            }
        }

        var cam = Camera.main;
        if (cam != null)
        {
            fallbackRayOrigin = cam.transform;
            Debug.Log("[CM] Ray origin: Camera.main (fallback)");
        }
        else
        {
            Debug.LogError("[CM] No ray origin found");
        }
    }

    private Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindDeepChild(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private void Update()
    {
        UpdatePreviewLine();
        UpdateAllConnectionLines();
    }

    private void SetupXRIUI()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas != null && canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
        {
            canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            Debug.Log("[CM] Added TrackedDeviceGraphicRaycaster to Canvas");
        }

        var eventSystem = FindObjectOfType<EventSystem>();
        if (eventSystem != null)
        {
            var standaloneModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (standaloneModule != null)
            {
                standaloneModule.enabled = false;
                Debug.Log("[CM] Disabled StandaloneInputModule");
            }

            if (eventSystem.GetComponent<XRUIInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<XRUIInputModule>();
                Debug.Log("[CM] Added XRUIInputModule to EventSystem");
            }
        }
    }

    private void OnActivatePerformed(InputAction.CallbackContext ctx)
    {
        HandleActivatePress();
    }

    private bool TryGetRay(out Ray ray)
    {
        if (rightNearFarInteractor != null && rightNearFarInteractor.curveOrigin != null)
        {
            var origin = rightNearFarInteractor.curveOrigin;
            ray = new Ray(origin.position, origin.forward);
            return true;
        }

        if (fallbackRayOrigin != null)
        {
            ray = new Ray(fallbackRayOrigin.position, fallbackRayOrigin.forward);
            return true;
        }

        ray = default;
        return false;
    }

    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];

    private void HandleActivatePress()
    {
        if (isOverUI)
            return;

        if (!TryGetRay(out Ray ray))
            return;

        if (codeManager != null && codeManager.IsExecuting)
        {
            Debug.Log("[CM] Blocked: code executing");
            return;
        }

        int hitCount = Physics.RaycastNonAlloc(ray, hitBuffer, maxPreviewDistance, blockLayerMask);
        Code hitBlock = null;

        for (int i = 0; i < hitCount; i++)
        {
            var block = hitBuffer[i].collider.GetComponentInParent<Code>();
            if (block != null)
            {
                hitBlock = block;
                break;
            }
        }

        if (hitBlock == null)
        {
        }
        else if (!IsConnectable(hitBlock))
        {
            Debug.Log($"[CM] Hit non-MoveCode: '{hitBlock.name}' ({hitBlock.GetType().Name})");
            hitBlock = null;
        }

        if (selectedBlock == null)
        {
            if (hitBlock != null && hitBlock is not BoolCode)
            {
                Debug.Log($"[CM] Select '{hitBlock.name}'");
                SelectBlock(hitBlock);
            }
        }
        else if (selectedBlock is While whileBlock && hitBlock is BoolCode boolBlock)
        {
            if (whileBlock.Judger == boolBlock)
            {
                Debug.Log($"[CM] Disconnect Judger '{whileBlock.name}' <- '{boolBlock.name}'");
                DisconnectJudger(whileBlock);
                AudioManager.Instance?.Play(SoundId.BlockDisconnect, whileBlock.transform.position);
            }
            else if (!IsAnyonesJudger(boolBlock))
            {
                Debug.Log($"[CM] Connect Judger '{whileBlock.name}' <- '{boolBlock.name}'");
                ConnectJudger(whileBlock, boolBlock);
                AudioManager.Instance?.Play(SoundId.BlockConnect, whileBlock.transform.position);
            }
            else
            {
                Debug.Log($"[CM] Judger reject: '{boolBlock.name}' already someone's Judger");
            }
            DeselectBlock();
        }
        else if (selectedBlock is If ifBlock && hitBlock is BoolCode boolBlock2)
        {
            if (ifBlock.Judger == boolBlock2)
            {
                Debug.Log($"[CM] Disconnect Judger '{ifBlock.name}' <- '{boolBlock2.name}'");
                DisconnectJudger(ifBlock);
                AudioManager.Instance?.Play(SoundId.BlockDisconnect, ifBlock.transform.position);
            }
            else if (!IsAnyonesJudger(boolBlock2))
            {
                Debug.Log($"[CM] Connect Judger '{ifBlock.name}' <- '{boolBlock2.name}'");
                ConnectJudger(ifBlock, boolBlock2);
                AudioManager.Instance?.Play(SoundId.BlockConnect, ifBlock.transform.position);
            }
            else
            {
                Debug.Log($"[CM] Judger reject: '{boolBlock2.name}' already someone's Judger");
            }
            DeselectBlock();
        }
        else if (selectedBlock is BoolCode && hitBlock is While hitWhile)
        {
            if (hitWhile.Judger == selectedBlock)
            {
                Debug.Log($"[CM] Disconnect Judger '{hitWhile.name}' <- '{selectedBlock.name}'");
                DisconnectJudger(hitWhile);
                AudioManager.Instance?.Play(SoundId.BlockDisconnect, hitWhile.transform.position);
                DeselectBlock();
            }
        }
        else if (selectedBlock is BoolCode && hitBlock is If hitIf)
        {
            if (hitIf.Judger == selectedBlock)
            {
                Debug.Log($"[CM] Disconnect Judger '{hitIf.name}' <- '{selectedBlock.name}'");
                DisconnectJudger(hitIf);
                AudioManager.Instance?.Play(SoundId.BlockDisconnect, hitIf.transform.position);
                DeselectBlock();
            }
        }
        else
        {
            if (hitBlock != null && hitBlock != selectedBlock)
            {
                if (TryConnect(selectedBlock, hitBlock))
                {
                    Debug.Log($"[CM] Connect '{selectedBlock.name}' -> '{hitBlock.name}'");
                    Connect(selectedBlock, hitBlock);
                    AudioManager.Instance?.Play(SoundId.BlockConnect, selectedBlock.transform.position);
                }
                else
                {
                    Debug.Log($"[CM] Rejected '{selectedBlock.name}' -> '{hitBlock.name}'");
                    AudioManager.Instance?.Play(SoundId.ValidationFail);
                }
                DeselectBlock();
            }
            else if (hitBlock == selectedBlock)
            {
                Debug.Log($"[CM] Disconnect self '{selectedBlock.name}'");
                bool hadLink = selectedBlock.next != null;
                Disconnect(selectedBlock);
                if (hadLink)
                    AudioManager.Instance?.Play(SoundId.BlockDisconnect, selectedBlock.transform.position);
                DeselectBlock();
            }
            else
            {
                Debug.Log($"[CM] Disconnect '{selectedBlock.name}' (no target)");
                bool hadLink = selectedBlock.next != null;
                Disconnect(selectedBlock);
                if (hadLink)
                    AudioManager.Instance?.Play(SoundId.BlockDisconnect, selectedBlock.transform.position);
                DeselectBlock();
            }
        }
    }

    private bool IsConnectable(Code block)
    {
        return block is MoveCode || block is TurnLeftCode || block is TurnRightCode || block is While || block is WhileEnd || block is BoolCode || block is If || block is Else || block is IfEnd || block is Start;
    }

    private void SelectBlock(Code block)
    {
        selectedBlock = block;
        block.SetHighlight(true);

        if (previewContainer == null)
        {
            CreatePreviewObjects();
        }
        previewContainer.SetActive(true);
    }

    private void DeselectBlock()
    {
        if (selectedBlock != null)
        {
            selectedBlock.SetHighlight(false);
            selectedBlock = null;
        }

        if (previewContainer != null)
        {
            previewContainer.SetActive(false);
        }
    }

    private bool TryConnect(Code from, Code to)
    {
        if (from == to) return false;
        if (WouldCreateCycle(from, to)) return false;
        if (IsAnyonesNext(to, exceptFrom: from)) return false;
        return true;
    }

    private bool WouldCreateCycle(Code from, Code to)
    {
        Code current = to;
        int maxIterations = 1000;
        int iterations = 0;

        while (current != null && iterations < maxIterations)
        {
            if (current == from) return true;
            current = current.next;
            iterations++;
        }

        return false;
    }

    private bool IsAnyonesNext(Code target, Code exceptFrom)
    {
        Code[] allBlocks = FindObjectsOfType<Code>();
        foreach (Code block in allBlocks)
        {
            if (block.next == target && block != exceptFrom)
            {
                return true;
            }
        }
        return false;
    }

    private void Connect(Code from, Code to)
    {
        Disconnect(from);

        from.next = to;

        CreateConnectionLine(from, to);
    }

    private void Disconnect(Code from)
    {
        Code oldNext = from.next;
        if (oldNext != null)
        {
            RemoveConnectionLine(from, oldNext);
        }

        from.next = null;
    }

    private bool IsAnyonesJudger(BoolCode target)
    {
        foreach (While w in FindObjectsOfType<While>())
            if (w.Judger == target) return true;
        foreach (If i in FindObjectsOfType<If>())
            if (i.Judger == target) return true;
        return false;
    }

    private void ConnectJudger(Code whileOrIfBlock, BoolCode boolBlock)
    {
        DisconnectJudger(whileOrIfBlock);
        if (whileOrIfBlock is While w) w.Judger = boolBlock;
        else if (whileOrIfBlock is If i) i.Judger = boolBlock;
        CreateJudgerLine(whileOrIfBlock, boolBlock);
    }

    private void DisconnectJudger(Code whileOrIfBlock)
    {
        BoolCode old = null;
        if (whileOrIfBlock is While w) old = w.Judger;
        else if (whileOrIfBlock is If i) old = i.Judger;
        if (old == null) return;
        RemoveConnectionLine(whileOrIfBlock, old);
        if (whileOrIfBlock is While w2) w2.Judger = null;
        else if (whileOrIfBlock is If i2) i2.Judger = null;
    }

    private void CreateJudgerLine(Code from, Code to)
    {
        GameObject lineObj = new GameObject($"Judger_{from.name}_to_{to.name}");
        lineObj.transform.SetParent(connectionsContainer.transform);

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.material = judgerLineMaterial;
        lr.startWidth = 0.005f;
        lr.endWidth = 0.005f;
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.SetPosition(0, from.transform.position);
        lr.SetPosition(1, to.transform.position);

        GameObject arrowhead = ArrowheadGenerator.CreateArrowhead(lineObj.transform, judgerLineMaterial);
        arrowhead.transform.position = (from.transform.position + to.transform.position) * 0.5f;
        arrowhead.transform.rotation = Quaternion.LookRotation((to.transform.position - from.transform.position).normalized);

        connections.Add(new ConnectionData { from = from, to = to, line = lr, arrowhead = arrowhead });
    }

    private void CreateConnectionLine(Code from, Code to)
    {
        GameObject lineObj = new GameObject($"Connection_{from.name}_to_{to.name}");
        lineObj.transform.SetParent(connectionsContainer.transform);

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.material = connectionLineMaterial;
        lr.startWidth = 0.005f;
        lr.endWidth = 0.005f;
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.SetPosition(0, from.transform.position);
        lr.SetPosition(1, to.transform.position);

        GameObject arrowhead = ArrowheadGenerator.CreateArrowhead(lineObj.transform, connectionLineMaterial);
        PlaceArrowheadOnSurface(from, to, arrowhead);

        connections.Add(new ConnectionData
        {
            from = from,
            to = to,
            line = lr,
            arrowhead = arrowhead
        });
    }

    private void RemoveConnectionLine(Code from, Code to)
    {
        for (int i = connections.Count - 1; i >= 0; i--)
        {
            if (connections[i].from == from && connections[i].to == to)
            {
                if (connections[i].line != null)
                {
                    Destroy(connections[i].line.gameObject);
                }
                connections.RemoveAt(i);
                return;
            }
        }
    }

    private void CreatePreviewObjects()
    {
        previewContainer = new GameObject("PreviewLine");
        previewContainer.transform.SetParent(transform);

        previewLineRenderer = previewContainer.AddComponent<LineRenderer>();
        previewLineRenderer.material = previewLineMaterial;
        previewLineRenderer.startWidth = 0.005f;
        previewLineRenderer.endWidth = 0.005f;
        previewLineRenderer.useWorldSpace = true;

        previewArrowhead = ArrowheadGenerator.CreateArrowhead(previewContainer.transform, previewLineMaterial);
    }

    private void UpdatePreviewLine()
    {
        if (selectedBlock == null || previewLineRenderer == null || previewContainer == null)
            return;

        if (!previewContainer.activeSelf) return;

        Vector3 startPos = selectedBlock.transform.position;

        if (!TryGetRay(out Ray ray))
            return;

        Vector3 endPos;
        if (Physics.Raycast(ray, out RaycastHit hit, maxPreviewDistance, blockLayerMask))
        {
            endPos = hit.point;
        }
        else
        {
            endPos = ray.GetPoint(maxPreviewDistance);
        }

        previewLineRenderer.SetPosition(0, startPos);
        previewLineRenderer.SetPosition(1, endPos);

        UpdateArrowhead(previewArrowhead, startPos, endPos);
    }

    private void UpdateAllConnectionLines()
    {
        for (int i = connections.Count - 1; i >= 0; i--)
        {
            ConnectionData data = connections[i];

            if (data.from == null || data.to == null)
            {
                if (data.line != null) Destroy(data.line.gameObject);
                connections.RemoveAt(i);
                continue;
            }

            Vector3 startPos = data.from.transform.position;
            Vector3 endPos = data.to.transform.position;

            data.line.SetPosition(0, startPos);
            data.line.SetPosition(1, endPos);

            PlaceArrowheadOnSurface(data.from, data.to, data.arrowhead);
        }
    }

    private void PlaceArrowheadOnSurface(Code from, Code to, GameObject arrowhead)
    {
        if (arrowhead == null) return;

        Vector3 fromCenter = from.transform.position;
        Vector3 toCenter = to.transform.position;
        Vector3 direction = (toCenter - fromCenter).normalized;

        arrowhead.transform.position = (fromCenter + toCenter) * 0.5f;
        arrowhead.transform.rotation = Quaternion.LookRotation(direction);
    }

    private void UpdateArrowhead(GameObject arrowhead, Vector3 from, Vector3 to)
    {
        if (arrowhead == null) return;

        Vector3 direction = (to - from).normalized;
        Vector3 midPoint = (from + to) * 0.5f;

        arrowhead.transform.position = midPoint;
        arrowhead.transform.rotation = Quaternion.LookRotation(direction);
    }

    public void ClearAllConnections()
    {
        DeselectBlock();

        for (int i = connections.Count - 1; i >= 0; i--)
        {
            if (connections[i].line != null)
                Destroy(connections[i].line.gameObject);
            if (connections[i].arrowhead != null)
                Destroy(connections[i].arrowhead);
        }

        connections.Clear();

        if (connectionsContainer != null)
        {
            for (int i = connectionsContainer.transform.childCount - 1; i >= 0; i--)
                Destroy(connectionsContainer.transform.GetChild(i).gameObject);
        }

        foreach (var code in FindObjectsOfType<Code>())
        {
            code.next = null;
            if (code is If ifBlock)
                ifBlock.Judger = null;
            else if (code is While whileBlock)
                whileBlock.Judger = null;
        }
    }

    public void CleanupBlock(Code block)
    {
        if (block == null)
            return;

        if (selectedBlock == block)
            DeselectBlock();

        Disconnect(block);

        foreach (var other in FindObjectsOfType<Code>())
        {
            if (other.next == block)
                Disconnect(other);
        }

        foreach (var whileBlock in FindObjectsOfType<While>())
        {
            if (whileBlock.Judger == block)
                DisconnectJudger(whileBlock);
        }

        foreach (var ifBlock in FindObjectsOfType<If>())
        {
            if (ifBlock.Judger == block)
                DisconnectJudger(ifBlock);
        }

        RemoveAllLinesForBlock(block);
    }

    private void RemoveAllLinesForBlock(Code block)
    {
        for (int i = connections.Count - 1; i >= 0; i--)
        {
            if (connections[i].from != block && connections[i].to != block)
                continue;

            if (connections[i].line != null)
                Destroy(connections[i].line.gameObject);

            connections.RemoveAt(i);
        }
    }
}
