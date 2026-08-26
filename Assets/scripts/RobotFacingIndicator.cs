using UnityEngine;

public class RobotFacingIndicator : MonoBehaviour
{
    public static RobotFacingIndicator Instance { get; private set; }

    [Header("Placement")]
    [Tooltip("Extra distance in world units beyond the robot surface.")]
    public float forwardPadding = 0.04f;
    [Tooltip("Arrow length as a fraction of robot diameter (slightly below 1).")]
    public float sizeRatio = 0.55f;

    [Header("Breathing")]
    public float breathSpeed = 2.5f;
    public float minAlpha = 0.45f;
    public float maxAlpha = 0.95f;
    public float minScaleMultiplier = 0.85f;
    public float maxScaleMultiplier = 1.15f;

    private GameObject indicatorRoot;
    private Transform arrowTransform;
    private Material indicatorMaterial;
    private bool isVisible;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private readonly Color baseColor = new Color(0.2f, 0.55f, 1f, 1f);

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (indicatorMaterial != null)
            Destroy(indicatorMaterial);
    }

    public void Show()
    {
        if (CodeManager.Robot == null)
            return;

        EnsureIndicatorCreated();
        if (indicatorRoot == null)
            return;

        Transform robot = CodeManager.Robot.transform;
        float robotScale = robot.lossyScale.x;

        indicatorRoot.transform.SetParent(robot, false);
        indicatorRoot.transform.localPosition = new Vector3(
            0f,
            GetRobotHeight(robot) / robotScale,
            GetForwardDistance(robot) / robotScale);
        indicatorRoot.transform.localRotation = Quaternion.identity;
        indicatorRoot.SetActive(true);
        isVisible = true;
    }

    public void Hide()
    {
        isVisible = false;
        if (indicatorRoot != null)
            indicatorRoot.SetActive(false);
    }

    private void Update()
    {
        if (!isVisible || arrowTransform == null || indicatorMaterial == null)
            return;

        float pulse = (Mathf.Sin(Time.time * breathSpeed) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(minAlpha, maxAlpha, pulse);
        float robotScale = CodeManager.Robot.transform.lossyScale.x;
        float worldScale = GetArrowWorldLength() * Mathf.Lerp(minScaleMultiplier, maxScaleMultiplier, pulse);

        Color color = baseColor;
        color.a = alpha;
        indicatorMaterial.SetColor(ColorId, color);
        indicatorMaterial.SetColor(EmissionColorId, baseColor * Mathf.Lerp(0.35f, 0.9f, pulse));

        arrowTransform.localScale = Vector3.one * (worldScale / robotScale);
    }

    private float GetRobotRadius(Transform robot)
    {
        var collider = robot.GetComponent<SphereCollider>();
        if (collider != null)
            return collider.radius * collider.transform.lossyScale.x;

        return 0.12f;
    }

    private float GetRobotHeight(Transform robot)
    {
        var collider = robot.GetComponent<SphereCollider>();
        if (collider != null)
            return collider.center.y * collider.transform.lossyScale.y;

        return 0.05f;
    }

    private float GetForwardDistance(Transform robot)
    {
        return GetRobotRadius(robot) + forwardPadding;
    }

    private float GetArrowWorldLength()
    {
        return GetRobotRadius(CodeManager.Robot.transform) * 2f * sizeRatio;
    }

    private void EnsureIndicatorCreated()
    {
        if (indicatorRoot != null)
            return;

        indicatorRoot = new GameObject("RobotFacingIndicator");
        indicatorRoot.transform.SetParent(transform, false);

        indicatorMaterial = CreateBlueBreathingMaterial();
        GameObject arrow = ArrowheadGenerator.CreateArrowhead(indicatorRoot.transform, indicatorMaterial);
        arrowTransform = arrow.transform;
        indicatorRoot.SetActive(false);
    }

    private static Material CreateBlueBreathingMaterial()
    {
        var material = new Material(Shader.Find("Standard"));
        material.SetFloat("_Mode", 3f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
        material.EnableKeyword("_EMISSION");
        material.SetColor("_Color", new Color(0.2f, 0.55f, 1f, 0.8f));
        material.SetColor("_EmissionColor", new Color(0.15f, 0.4f, 0.85f, 1f));
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return material;
    }
}
