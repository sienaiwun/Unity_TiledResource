namespace UnityEngine.Rendering.LWRP
{
    internal class BlendWeightedRevealagePass : ScriptableRenderPass
    {

        FilteringSettings m_FilteringSettings;
        const string m_ProfilerTag = "Render OIT Revealage";
        ShaderTagId m_ShaderTagId = new ShaderTagId("OITRevealage");
        //ShaderTagId m_ShaderTagId = new ShaderTagId("LightweightForward");

        int kDepthBufferBits = 0;

        RenderTargetHandle revealageHandle;
        RenderTargetHandle depthHandle;

        //internal RenderTextureDescriptor descriptor { get; private set; }
        RenderTextureDescriptor descriptor;

        public BlendWeightedRevealagePass(RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask)
        {
            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            renderPassEvent = evt;
        }

        public void Setup(RenderTextureDescriptor baseDescriptor, RenderTargetHandle revealageHandle, RenderTargetHandle depthHandle)
        {
            this.revealageHandle = revealageHandle;
            this.depthHandle = depthHandle;

            baseDescriptor.colorFormat = RenderTextureFormat.RHalf;
            baseDescriptor.depthBufferBits = kDepthBufferBits;
            //baseDescriptor.bindMS = true;
            baseDescriptor.msaaSamples = 1;
            descriptor = baseDescriptor;

            //depthNormalsMaterial =
            //    CoreUtils.CreateEngineMaterial("Hidden/Internal-DepthNormalsTexture");

            //descriptor = new RenderTextureDescriptor();
            //descriptor.bindMS = false;
            //descriptor.colorFormat = RenderTextureFormat.RHalf;
            //descriptor.depthBufferBits = kDepthBufferBits;
            //descriptor.width = baseDescriptor.width;
            //descriptor.height = baseDescriptor.height;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            //cameraTextureDescriptor.colorFormat = RenderTextureFormat.ARGBHalf;
            //cameraTextureDescriptor.depthBufferBits = kDepthBufferBits;

            cmd.GetTemporaryRT(revealageHandle.id, descriptor, FilterMode.Bilinear);
            //ConfigureTarget(revealageHandle.Identifier());
            ConfigureTarget(revealageHandle.Identifier(), depthHandle.Identifier());
            ConfigureClear(ClearFlag.Color, Color.white);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {

            CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);

            using (new ProfilingSample(cmd, m_ProfilerTag))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                //var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                var drawSettings = CreateDrawingSettings(m_ShaderTagId, ref renderingData, SortingCriteria.CommonTransparent);
                


                ref CameraData cameraData = ref renderingData.cameraData;
                Camera camera = cameraData.camera;

                context.DrawRenderers(renderingData.cullResults, ref drawSettings,
                    ref m_FilteringSettings);

                cmd.SetGlobalTexture("_RevealageTex", revealageHandle.id);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            //if (cmd == null)
            //    throw new ArgumentNullException("cmd");

            if (revealageHandle != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(revealageHandle.id);
                revealageHandle = RenderTargetHandle.CameraTarget;
            }
        }
    }
}


