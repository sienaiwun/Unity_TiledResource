using System;
using UnityEngine;
using UnityEngine.Rendering.LWRP;
using UnityEngine.Rendering;

public class DepthNormals : ScriptableRendererFeature
{
    [System.Serializable]
    public class DepthNormalsSettings
    {
        public RenderPassEvent Event = RenderPassEvent.AfterRenderingPrePasses;
        public LayerMask renderLayerMask;
        public int RenderTextureResolution = 1024;
    }

    public DepthNormalsSettings settings = new DepthNormalsSettings();
    private DepthNormalsPass depthNormalsPass;

    public override void Create()
    {
        if (depthNormalsPass == null)
            depthNormalsPass = new DepthNormalsPass(settings.Event, RenderQueueRange.opaque, settings.renderLayerMask);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        int width, height;
        if (settings.RenderTextureResolution <= 0)
        {
            width = 16;
            height = 16;
        }

        else
        {
            width = settings.RenderTextureResolution;
            height = width;
        }

        depthNormalsPass.Setup(new RenderTextureDescriptor(width, height), new RenderTargetHandle());
        renderer.EnqueuePass(depthNormalsPass);
    }

    void OnValidate()
    {
        depthNormalsPass?.SetLayerMask(settings.renderLayerMask);
    }
}


public class DepthNormalsPass : ScriptableRenderPass
{
    int kDepthBufferBits = 32;

    private RenderTargetHandle depthAttachmentHandle { get; set; }
    internal RenderTextureDescriptor descriptor { get; private set; }

    private FilteringSettings m_FilteringSettings;
    string m_ProfilerTag = "Depth + Normals Prepass";
    ShaderTagId m_ShaderTagId = new ShaderTagId("LightweightForward");

    private Material depthNormalsMaterial = null;

    public DepthNormalsPass(RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask)
    {
        m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
        renderPassEvent = evt;
        depthNormalsMaterial =
            CoreUtils.CreateEngineMaterial("Hidden/Internal-DepthNormalsTexture");
    }


    public void Setup(RenderTextureDescriptor baseDescriptor, RenderTargetHandle depthAttachmentHandle)
    {
        this.depthAttachmentHandle = depthAttachmentHandle;
        baseDescriptor.colorFormat = RenderTextureFormat.ARGB32;
        baseDescriptor.depthBufferBits = kDepthBufferBits;
        descriptor = baseDescriptor;
    }


    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        cmd.GetTemporaryRT(depthAttachmentHandle.id, descriptor, FilterMode.Point);
        ConfigureTarget(depthAttachmentHandle.Identifier());
        ConfigureClear(ClearFlag.All, Color.black);
    }


    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);

        using (new ProfilingSample(cmd, m_ProfilerTag))
        {
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
            var drawSettings = CreateDrawingSettings(m_ShaderTagId, ref renderingData, sortFlags);
            drawSettings.perObjectData = PerObjectData.None;


            ref CameraData cameraData = ref renderingData.cameraData;
            Camera camera = cameraData.camera;
            if (cameraData.isStereoEnabled)
                context.StartMultiEye(camera);


            drawSettings.overrideMaterial = depthNormalsMaterial;

            context.DrawRenderers(renderingData.cullResults, ref drawSettings,
                ref m_FilteringSettings);

            cmd.SetGlobalTexture("_CameraDepthNormalsTexture", depthAttachmentHandle.id);
        }

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public void SetLayerMask(LayerMask mask)
    {
        m_FilteringSettings.layerMask = mask;
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        if (cmd == null)
            throw new ArgumentNullException("cmd");

        if (depthAttachmentHandle != RenderTargetHandle.CameraTarget)
        {
            cmd.ReleaseTemporaryRT(depthAttachmentHandle.id);
            depthAttachmentHandle = RenderTargetHandle.CameraTarget;
        }
    }
}
