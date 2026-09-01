using System;
using UnityEngine;

public abstract class Code : MonoBehaviour
{
    public Code next = null;
    public event Action OnComplete;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly Color HighlightColor = Color.yellow;

    private MaterialPropertyBlock highlightBlock;

    public abstract void work();

    protected virtual void Awake()
    {
        ApplyCodeLayer();

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


