using UnityEngine;

public static class ArrowheadGenerator
{
    private static Mesh sharedMesh;

    private static Mesh GetSharedMesh()
    {
        if (sharedMesh == null)
            sharedMesh = CreateArrowheadMesh();
        return sharedMesh;
    }

    public static GameObject CreateArrowhead(Transform parent, Material material)
    {
        GameObject arrow = new GameObject("Arrowhead");

        MeshFilter mf = arrow.AddComponent<MeshFilter>();
        MeshRenderer mr = arrow.AddComponent<MeshRenderer>();

        mf.mesh = GetSharedMesh();

        if (material != null)
        {
            mr.material = material;
        }

        arrow.transform.SetParent(parent);
        arrow.transform.localPosition = Vector3.zero;
        arrow.transform.localRotation = Quaternion.identity;
        arrow.transform.localScale = Vector3.one * 0.06f;

        return arrow;
    }

    private static Mesh CreateArrowheadMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "Arrowhead";

        Vector3 tip = new Vector3(0f, 0f, 0.5f);
        Vector3 bottomCenter = new Vector3(0f, 0f, -0.5f);
        Vector3 bottomLeft = new Vector3(-0.4f, 0f, -0.5f);
        Vector3 bottomRight = new Vector3(0.4f, 0f, -0.5f);
        Vector3 bottomTop = new Vector3(0f, 0.4f, -0.5f);
        Vector3 bottomBottom = new Vector3(0f, -0.4f, -0.5f);

        Vector3[] vertices = new Vector3[]
        {
            tip,
            bottomTop,
            bottomRight,
            bottomCenter,
            bottomBottom,
            bottomLeft
        };

        int[] triangles = new int[]
        {
            0, 1, 2,
            0, 2, 3,
            0, 3, 4,
            0, 4, 5,
            0, 5, 1,
            1, 5, 4, 1, 4, 3, 1, 3, 2
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}
