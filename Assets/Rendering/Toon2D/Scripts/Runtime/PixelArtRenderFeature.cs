using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class PixelArtRenderFeature : ScriptableRendererFeature
{
    [Serializable]
    public sealed class PixelArtSettings
    {
        public Material material;
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        [Range(96, 720)] public int virtualHeight = 320;
        [Range(0, 64)] public int colorSteps;
        [Range(0f, 4f)] public float edgeStrength;
        [Range(0f, 2f)] public float saturation = 1f;
        public bool affectSceneView;
    }

    private sealed class PixelArtPass : ScriptableRenderPass
    {
        private static readonly int VirtualHeightId = Shader.PropertyToID("_VirtualHeight");
        private static readonly int ColorStepsId = Shader.PropertyToID("_ColorSteps");
        private static readonly int EdgeStrengthId = Shader.PropertyToID("_EdgeStrength");
        private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        private readonly ProfilingSampler pixelArtSampler = new ProfilingSampler("Pixel Art");
        private readonly PixelArtSettings settings;
        private RTHandle source;
        private RTHandle tempColor;

        public PixelArtPass(PixelArtSettings settings)
        {
            this.settings = settings;
        }

        public void Setup(RTHandle source)
        {
            this.source = source;
            renderPassEvent = settings.renderPassEvent;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            var descriptor = cameraTextureDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            RenderingUtils.ReAllocateIfNeeded(
                ref tempColor,
                descriptor,
                FilterMode.Point,
                TextureWrapMode.Clamp,
                name: "_PixelArtColor");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (settings.material == null || source == null || tempColor == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get("Pixel Art");
            using (new ProfilingScope(cmd, pixelArtSampler))
            {
                settings.material.SetFloat(VirtualHeightId, settings.virtualHeight);
                settings.material.SetFloat(ColorStepsId, settings.colorSteps);
                settings.material.SetFloat(EdgeStrengthId, settings.edgeStrength);
                settings.material.SetFloat(SaturationId, settings.saturation);

                Blitter.BlitCameraTexture(cmd, source, tempColor, settings.material, 0);
                Blitter.BlitCameraTexture(cmd, tempColor, source);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            tempColor?.Release();
            tempColor = null;
        }
    }

    public PixelArtSettings settings = new PixelArtSettings();
    private PixelArtPass pass;

    public override void Create()
    {
        pass = new PixelArtPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.material == null)
        {
            return;
        }

        var cameraType = renderingData.cameraData.cameraType;
        if (cameraType != CameraType.Game && !(settings.affectSceneView && cameraType == CameraType.SceneView))
        {
            return;
        }

        renderer.EnqueuePass(pass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (settings.material == null)
        {
            return;
        }

        pass.Setup(renderer.cameraColorTargetHandle);
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Dispose();
        pass = null;
    }
}
