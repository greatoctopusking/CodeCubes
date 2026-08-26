using System;
using UnityEngine;

public abstract class Code : MonoBehaviour
{
    public Code next = null;
    public event Action OnComplete;

    public abstract void work();

    protected virtual void Awake()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.constraints |= RigidbodyConstraints.FreezeRotation;
        }
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
        if (renderer != null) renderer.material.color = active ? Color.yellow : Color.white;
    }
}

public abstract class BoolCode : Code
{
    public bool judge = false;

}


