using System;
using UnityEngine;

public abstract class Code : MonoBehaviour
{
    public Code next = null;
    public event Action OnComplete;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    private static readonly Color HighlightColor = Color.yellow;
    private const float StartMatchedEmission = 0.8f;

    private MaterialPropertyBlock highlightBlock;

    public abstract void work();

    protected virtual void Awake()
    {
        ApplyCodeLayer();
        MatchStartBlockBrightness();

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.constraints |= RigidbodyConstraints.FreezeRotation;
        }
    }

    private void ApplyCodeLayer()
    {
        int codeLayer = LayerMask.NameToLayer("Code");
        if (codeLayer < 0)
            return;

        SetLayerRecursively(transform, codeLayer);
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }

    private void MatchStartBlockBrightness()
    {
        if (this is Start)
            return;

        var renderers = GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null)
                continue;

            var materials = renderer.materials;
            for (int m = 0; m < materials.Length; m++)
            {
                var material = materials[m];
                if (material == null || IsTextMaterial(material))
                    continue;

                Color albedo = Color.white;
                if (material.HasProperty(BaseColorId))
                    albedo = material.GetColor(BaseColorId);
                else if (material.HasProperty(ColorId))
                    albedo = material.GetColor(ColorId);

                if (material.HasProperty(EmissionColorId))
                {
                    Color emission = albedo * StartMatchedEmission;
                    emission.a = albedo.a;
                    material.EnableKeyword("_EMISSION");
                    material.SetColor(EmissionColorId, emission);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }

                if (material.HasProperty(MetallicId))
                    material.SetFloat(MetallicId, Mathf.Min(material.GetFloat(MetallicId), 0.2f));
            }
        }
    }

    private static bool IsTextMaterial(Material material)
    {
        if (material.shader == null)
            return false;

        string shaderName = material.shader.name;
        return shaderName.IndexOf("Font", StringComparison.OrdinalIgnoreCase) >= 0
            || shaderName.IndexOf("GUI", StringComparison.OrdinalIgnoreCase) >= 0
            || shaderName.IndexOf("Text", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    protected void Complete()
    {
        OnComplete?.Invoke();
    }

    public void ResetState()
    {
    }

    public void SetHighlight(bool active)
    {
        var renderers = GetComponentsInChildren<MeshRenderer>(true);
        if (!active)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].SetPropertyBlock(null);
            }
            return;
        }

        if (highlightBlock == null)
            highlightBlock = new MaterialPropertyBlock();

        for (int i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(highlightBlock);
            highlightBlock.SetColor(BaseColorId, HighlightColor);
            highlightBlock.SetColor(ColorId, HighlightColor);
            renderer.SetPropertyBlock(highlightBlock);
        }
    }
}

public abstract class BoolCode : Code
{
    public bool judge = false;

}


