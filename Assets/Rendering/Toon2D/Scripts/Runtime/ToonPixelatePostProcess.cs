using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public sealed class ToonPixelatePostProcess : MonoBehaviour
{
    public Shader pixelateShader;
    [Range(144, 720)] public int referenceHeight = 320;
    [Range(8, 96)] public float colorSteps = 40f;
    [Range(0f, 0.05f)] public float ditherStrength = 0.012f;

    private Material pixelateMaterial;

    private void OnEnable()
    {
        if (pixelateShader == null)
        {
            pixelateShader = Shader.Find("Hidden/Toon2D/PixelatePostProcess");
        }
    }

    private void OnDisable()
    {
        if (pixelateMaterial != null)
        {
            DestroyImmediate(pixelateMaterial);
            pixelateMaterial = null;
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        var targetHeight = Mathf.Clamp(referenceHeight, 144, Mathf.Max(144, source.height));
        var targetWidth = Mathf.Max(1, Mathf.RoundToInt(source.width * (targetHeight / (float)source.height)));

        var lowResolution = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, source.format);
        lowResolution.filterMode = FilterMode.Point;
        lowResolution.wrapMode = TextureWrapMode.Clamp;

        Graphics.Blit(source, lowResolution);

        if (pixelateShader == null)
        {
            Graphics.Blit(lowResolution, destination);
            RenderTexture.ReleaseTemporary(lowResolution);
            return;
        }

        if (pixelateMaterial == null)
        {
            pixelateMaterial = new Material(pixelateShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        pixelateMaterial.SetFloat("_ColorSteps", colorSteps);
        pixelateMaterial.SetFloat("_DitherStrength", ditherStrength);
        Graphics.Blit(lowResolution, destination, pixelateMaterial);
        RenderTexture.ReleaseTemporary(lowResolution);
    }
}
