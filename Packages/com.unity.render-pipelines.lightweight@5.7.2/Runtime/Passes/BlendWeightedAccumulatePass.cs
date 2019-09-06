namespace UnityEngine.Rendering.LWRP
{

    internal class BlendWeightedAccumulatePass : ScriptableRenderPass
    {
        FilteringSettings m_FilteringSettings;
        const string m_ProfilerTag = "Render OIT Accumulate";
        ShaderTagId m_ShaderTagId = new ShaderTagId("OITAccumulate");
        //ShaderTagId m_ShaderTagId = new ShaderTagId("LightweightForward");

        int kDepthBufferBits = 0;

        RenderTargetHandle accumulateHandle;
        RenderTargetHandle depthHandle;

        //internal RenderTextureDescriptor descriptor { get; set; }

        RenderTextureDescriptor descriptor;

        public BlendWeightedAccumulatePass(RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask)
        {
            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            renderPassEvent = evt;
            //accumulateHandle.Init("_AccumTex");
        }

        public void Setup(RenderTextureDescriptor baseDescriptor, RenderTargetHandle accumulateHandle, RenderTargetHandle depthHandle)
        {
            this.accumulateHandle = accumulateHandle;
            this.depthHandle = depthHandle;

            baseDescriptor.colorFormat = RenderTextureFormat.ARGBHalf;
            baseDescriptor.depthBufferBits = kDepthBufferBits;
            //baseDescriptor.bindMS = true;
            baseDescriptor.msaaSamples = 1;
            descriptor = baseDescriptor;

            //depthNormalsMaterial =
            //    CoreUtils.CreateEngineMaterial("Hidden/Internal-DepthNormalsTexture");

            //descriptor = new RenderTextureDescriptor();
            //descriptor.bindMS = false;
            //descriptor.colorFormat = RenderTextureFormat.ARGBHalf;
            //descriptor.depthBufferBits = kDepthBufferBits;
            //descriptor.width = baseDescriptor.width;
            //descriptor.height = baseDescriptor.height;


        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            //cameraTextureDescriptor.colorFormat = RenderTextureFormat.ARGBHalf;
            //cameraTextureDescriptor.depthBufferBits = kDepthBufferBits;

            cmd.GetTemporaryRT(accumulateHandle.id, descriptor, FilterMode.Bilinear);
            //ConfigureTarget(accumulateHandle.Identifier());
            ConfigureTarget(accumulateHandle.Identifier(), depthHandle.Identifier());
            ConfigureClear(ClearFlag.Color, Color.black);
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
                //drawSettings.perObjectData = PerObjectData.None;


                ref CameraData cameraData = ref renderingData.cameraData;
                Camera camera = cameraData.camera;

                context.DrawRenderers(renderingData.cullResults, ref drawSettings,
                    ref m_FilteringSettings);

                cmd.SetGlobalTexture("_AccumTex", accumulateHandle.id);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            //if (cmd == null)
            //    throw new ArgumentNullException("cmd");

            if (accumulateHandle != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(accumulateHandle.id);
                accumulateHandle = RenderTargetHandle.CameraTarget;
            }
        }
    }

}
