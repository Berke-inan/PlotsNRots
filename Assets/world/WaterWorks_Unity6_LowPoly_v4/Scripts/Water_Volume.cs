using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

/// <summary>
/// Unity 6 / URP 17 compatible fullscreen water-volume renderer feature.
/// Supports the default Render Graph path and Compatibility Mode.
/// </summary>
public sealed class Water_Volume : ScriptableRendererFeature
{
    [System.Serializable]
    public sealed class Settings
    {
        public Material material;
        public RenderPassEvent renderPass = RenderPassEvent.BeforeRenderingPostProcessing;
        public bool showInSceneView = true;
    }

    private sealed class WaterVolumePass : ScriptableRenderPass
    {
        private const string PassName = "Low Poly Water Volume";
        private const string TemporaryTextureName = "_WaterVolumeTemporaryColorTexture";

        private readonly Material _material;
        private readonly bool _showInSceneView;

        // Compatibility Mode only.
        private RTHandle _source;
        private RTHandle _temporaryColorTexture;

        public WaterVolumePass(Material material, bool showInSceneView)
        {
            _material = material;
            _showInSceneView = showInSceneView;

            // The low-poly volume shader reconstructs world position from camera depth.
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        // Unity 6 uses CameraData on the compatibility path and
        // UniversalCameraData on the Render Graph path. Keep the actual
        // filtering in one Camera-based method so both APIs are supported.
        private bool ShouldRender(Camera camera)
        {
            if (_material == null || camera == null)
                return false;

            CameraType cameraType = camera.cameraType;

            if (cameraType == CameraType.Reflection ||
                cameraType == CameraType.Preview)
                return false;

            if (!_showInSceneView && cameraType == CameraType.SceneView)
                return false;

            return true;
        }

        private bool ShouldRender(CameraData cameraData)
        {
            return ShouldRender(cameraData.camera);
        }

        private bool ShouldRender(UniversalCameraData cameraData)
        {
            return ShouldRender(cameraData.camera);
        }

        public void SetTarget(RTHandle source)
        {
            _source = source;
        }

        // Compatibility Mode (Render Graph disabled).
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (_source == null || !ShouldRender(renderingData.cameraData))
                return;

            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;

            RenderingUtils.ReAllocateIfNeeded(
                ref _temporaryColorTexture,
                descriptor,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: TemporaryTextureName);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!ShouldRender(renderingData.cameraData) ||
                _source == null ||
                _temporaryColorTexture == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get(PassName);

            // Unity 6's Blitter binds the source as _BlitTexture.
            Blitter.BlitCameraTexture(cmd, _source, _temporaryColorTexture, _material, 0);
            Blitter.BlitCameraTexture(cmd, _temporaryColorTexture, _source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            _source = null;
        }

        // Unity 6 / URP 17 Render Graph path.
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (!ShouldRender(cameraData))
                return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            // A readable intermediate color texture is required for a material blit.
            if (resourceData.isActiveTargetBackBuffer)
                return;

            TextureHandle source = resourceData.activeColorTexture;

            TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
            destinationDesc.name = TemporaryTextureName;
            destinationDesc.clearBuffer = false;
            destinationDesc.depthBufferBits = 0;

            TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

            RenderGraphUtils.BlitMaterialParameters parameters =
                new RenderGraphUtils.BlitMaterialParameters(source, destination, _material, 0);

            renderGraph.AddBlitPass(parameters, PassName);

            // Avoid a second copy in Render Graph; later passes use this as camera color.
            resourceData.cameraColor = destination;
        }

        public void Dispose()
        {
            _temporaryColorTexture?.Release();
            _temporaryColorTexture = null;
            _source = null;
        }
    }

    public Settings settings = new Settings();

    private WaterVolumePass _pass;

    public override void Create()
    {
        if (settings.material == null)
            settings.material = Resources.Load<Material>("Water_Volume");

        _pass?.Dispose();
        _pass = new WaterVolumePass(settings.material, settings.showInSceneView)
        {
            renderPassEvent = settings.renderPass
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_pass == null || settings.material == null)
            return;

        renderer.EnqueuePass(_pass);
    }

    // Required for the Compatibility Mode target handle.
    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (_pass == null || settings.material == null)
            return;

        _pass.SetTarget(renderer.cameraColorTargetHandle);
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.Dispose();
        _pass = null;
    }
}
