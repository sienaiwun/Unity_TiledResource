using System;

namespace UnityEngine.Rendering.LWRP
{
    /// <summary>
    /// Render all objects that have a 'DepthOnly' pass into the given depth buffer.
    ///
    /// You can use this pass to prime a depth buffer for subsequent rendering.
    /// Use it as a z-prepass, or use it to generate a depth buffer.
    /// </summary>
    internal class NormalOnlyPass : ScriptableRenderPass
    {
        int kDepthBufferBits = 32;

        private RenderTargetHandle normalAttachmentHandle { get; set; }
        private RenderTargetHandle cameraColorAttachmentHandle { get; set; }
        private RenderTargetHandle cameraDepthAttachmentHandle { get; set; }
        internal RenderTextureDescriptor descriptor { get; private set; }

        private FilteringSettings m_FilteringSettings;
        string m_ProfilerTag = "Normals Prepass";
        ShaderTagId m_ShaderTagId = new ShaderTagId("LightweightForward");

        private Material normalsMaterial = null;

        public NormalOnlyPass(RenderPassEvent evt, RenderQueueRange renderQueueRange)
        {
            m_FilteringSettings = new FilteringSettings(renderQueueRange);
            renderPassEvent = evt;
            normalsMaterial =
               CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/Roystan/Normals Texture"));
        }

        //public void Setup(RenderTextureDescriptor baseDescriptor, RenderTargetHandle normalAttachmentHandle, RenderTargetHandle cameraColorAttachmentHandle, RenderTargetHandle cameraDepthAttachmentHandle)
        public void Setup(RenderTextureDescriptor baseDescriptor, RenderTargetHandle normalAttachmentHandle)
        {
            this.normalAttachmentHandle = normalAttachmentHandle;
            //this.cameraDepthAttachmentHandle = cameraDepthAttachmentHandle;
            //this.cameraColorAttachmentHandle = cameraColorAttachmentHandle;
            baseDescriptor.colorFormat = RenderTextureFormat.ARGB32;
            baseDescriptor.depthBufferBits = kDepthBufferBits;
            descriptor = baseDescriptor;
        }


        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            cmd.GetTemporaryRT(normalAttachmentHandle.id, descriptor, FilterMode.Point);
            //ConfigureTarget(normalAttachmentHandle.Identifier(), cameraDepthAttachmentHandle.Identifier());
            ConfigureTarget(normalAttachmentHandle.Identifier());
            ConfigureClear(ClearFlag.All, Color.black);
            //ConfigureClear(ClearFlag.Color, Color.black);
        }


        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);

            using (new ProfilingSample(cmd, m_ProfilerTag))
            {
                //cmd.GetTemporaryRT(normalAttachmentHandle.id, descriptor, FilterMode.Point);
                //context.ExecuteCommandBuffer(cmd);
                //ConfigureClear(ClearFlag.Color, Color.black);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                var drawSettings = CreateDrawingSettings(m_ShaderTagId, ref renderingData, sortFlags);
                drawSettings.perObjectData = PerObjectData.None;


                ref CameraData cameraData = ref renderingData.cameraData;
                Camera camera = cameraData.camera;
                if (cameraData.isStereoEnabled)
                    context.StartMultiEye(camera);


                drawSettings.overrideMaterial = normalsMaterial;
                //m_FilteringSettings.layerMask = 1 << LayerMask.NameToLayer("Character");

                context.DrawRenderers(renderingData.cullResults, ref drawSettings,
                    ref m_FilteringSettings);

                cmd.SetGlobalTexture("_CameraNormalsTexture", normalAttachmentHandle.id);
                //cmd.SetRenderTarget(cameraColorAttachmentHandle.Identifier(), RenderBufferLoadAction.Load, RenderBufferStoreAction.StoreAndResolve, cameraDepthAttachmentHandle.Identifier(), RenderBufferLoadAction.Load, RenderBufferStoreAction.StoreAndResolve);
                //cmd.ReleaseTemporaryRT(normalAttachmentHandle.id);
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

            if (normalAttachmentHandle != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(normalAttachmentHandle.id);
                normalAttachmentHandle = RenderTargetHandle.CameraTarget;
            }
        }
    }
}
