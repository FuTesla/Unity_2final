using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public sealed class ToonScreenSpaceOutline : MonoBehaviour
{
    public Shader outlineShader;
    public Color outlineColor = new Color(0.018f, 0.017f, 0.02f, 1f);
    [Range(0.5f, 3f)] public float thickness = 1f;
    [Range(0.001f, 0.08f)] public float depthThreshold = 0.012f;
    [Range(0.01f, 1f)] public float normalThreshold = 0.18f;
    [Range(0f, 1f)] public float strength = 0.85f;

    private Camera targetCamera;
    private Material outlineMaterial;

    private void OnEnable()
    {
        targetCamera = GetComponent<Camera>();
        targetCamera.depthTextureMode |= DepthTextureMode.DepthNormals;

        if (outlineShader == null)
        {
            outlineShader = Shader.Find("Hidden/Toon2D/ScreenSpaceOutline");
        }
    }

    private void OnDisable()
    {
        if (outlineMaterial != null)
        {
            DestroyImmediate(outlineMaterial);
            outlineMaterial = null;
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (outlineShader == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        if (outlineMaterial == null)
        {
            outlineMaterial = new Material(outlineShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        outlineMaterial.SetColor("_OutlineColor", outlineColor);
        outlineMaterial.SetFloat("_Thickness", thickness);
        outlineMaterial.SetFloat("_DepthThreshold", depthThreshold);
        outlineMaterial.SetFloat("_NormalThreshold", normalThreshold);
        outlineMaterial.SetFloat("_Strength", strength);
        Graphics.Blit(source, destination, outlineMaterial);
    }
}
