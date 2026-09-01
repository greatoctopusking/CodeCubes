using System;
using UnityEngine;

public abstract class Code : MonoBehaviour
{
    public Code next = null;
    public event Action OnComplete;

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
        var renderer = GetComponent<Renderer>();
        if (renderer == null)
            renderer = GetComponentInChildren<Renderer>();
        if (renderer != null) renderer.material.color = active ? Color.yellow : Color.white;
    }
}

public abstract class BoolCode : Code
{
    public bool judge = false;

}


