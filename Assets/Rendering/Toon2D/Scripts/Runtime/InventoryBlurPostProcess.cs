using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public sealed class InventoryBlurPostProcess : MonoBehaviour
{
    public Shader blurShader;
    [Range(0f, 1f)] public float intensity;
    [Range(1, 4)] public int iterations = 2;
    [Range(1f, 6f)] public float spread = 2.2f;

    private Material blurMaterial;

    private void OnEnable()
    {
        if (blurShader == null)
        {
            blurShader = Shader.Find("Hidden/Toon2D/InventoryBlurPostProcess");
        }
    }

    private void OnDisable()
    {
        if (blurMaterial != null)
        {
            DestroyImmediate(blurMaterial);
            blurMaterial = null;
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (intensity <= 0.001f || blurShader == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        if (blurMaterial == null)
        {
            blurMaterial = new Material(blurShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        var width = Mathf.Max(1, source.width / 2);
        var height = Mathf.Max(1, source.height / 2);
        var bufferA = RenderTexture.GetTemporary(width, height, 0, source.format);
        var bufferB = RenderTexture.GetTemporary(width, height, 0, source.format);
        bufferA.filterMode = FilterMode.Bilinear;
        bufferB.filterMode = FilterMode.Bilinear;

        Graphics.Blit(source, bufferA);

        var passCount = Mathf.Clamp(iterations, 1, 4);
        for (var i = 0; i < passCount; i++)
        {
            blurMaterial.SetVector("_BlurOffset", new Vector4(spread * intensity / width, 0f, 0f, 0f));
            Graphics.Blit(bufferA, bufferB, blurMaterial);
            blurMaterial.SetVector("_BlurOffset", new Vector4(0f, spread * intensity / height, 0f, 0f));
            Graphics.Blit(bufferB, bufferA, blurMaterial);
        }

        Graphics.Blit(bufferA, destination);
        RenderTexture.ReleaseTemporary(bufferA);
        RenderTexture.ReleaseTemporary(bufferB);
    }
}
