using UnityEngine;

public class StarRemainCode : BoolCode
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");

    protected override void Awake()
    {
        base.Awake();
        MatchOtherBlockBrightness();
    }

    private void MatchOtherBlockBrightness()
    {
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
                if (material == null)
                    continue;

                if (material.HasProperty(EmissionColorId))
                    material.SetColor(EmissionColorId, Color.black);

                material.DisableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;

                if (material.HasProperty(MetallicId))
                    material.SetFloat(MetallicId, Mathf.Min(material.GetFloat(MetallicId), 0.5f));

                if (material.HasProperty(SmoothnessId))
                    material.SetFloat(SmoothnessId, Mathf.Min(material.GetFloat(SmoothnessId), 0.4f));

                if (material.HasProperty(GlossinessId))
                    material.SetFloat(GlossinessId, Mathf.Min(material.GetFloat(GlossinessId), 0.4f));
            }
        }
    }

    public override void work()
    {
        if (LevelManager.Instance == null)
        {
            judge = false;
            Complete();
            return;
        }

        foreach (var star in Object.FindObjectsOfType<Star>())
        {
            if (!star.collected)
            {
                judge = true;
                Complete();
                return;
            }
        }

        judge = false;
        Complete();
    }
}
