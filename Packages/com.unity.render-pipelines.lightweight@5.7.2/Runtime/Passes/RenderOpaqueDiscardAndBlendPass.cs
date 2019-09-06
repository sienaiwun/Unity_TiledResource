using System.Collections.Generic;

namespace UnityEngine.Rendering.LWRP
{
    /// <summary>
    /// Render all opaque forward objects into the given color and depth target
    ///
    /// You can use this pass to render objects that have a material and/or shader
    /// with the pass names LightweightForward or SRPDefaultUnlit. The pass only
    /// renders objects in the rendering queue range of Opaque objects.
    /// </summary>
    internal class RenderOpaqueDiscardAndBlendPass : ScriptableRenderPass
    {
        FilteringSettings m_FilteringSettings;
        const string m_ProfilerTag = "Render Discard and Blend";
        private List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>();

        public RenderOpaqueDiscardAndBlendPass(RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask)
        {
            m_ShaderTagIdList.Add(new ShaderTagId("LightweightDiscard"));
            m_ShaderTagIdList.Add(new ShaderTagId("LightweightBlend"));
            renderPassEvent = evt;
            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            const string semiTranShadowKeywords = "_SEMITRANSPARENT_SHADOWS";
            CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
            using (new ProfilingSample(cmd, m_ProfilerTag))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                Camera camera = renderingData.cameraData.camera;
                var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                var drawSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, sortFlags);
                CoreUtils.SetKeyword(cmd, semiTranShadowKeywords, renderingData.shadowData.supportsSemiTransShadow);

                if (IsAfterPP)
                {
                    SetRenderTarget(
                        cmd,
                        BuiltinRenderTextureType.CameraTarget,
                        RenderBufferLoadAction.Load,
                        RenderBufferStoreAction.Store,
                        ClearFlag.None,
                        Color.black,
                        TargetDimension);
                }

                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref m_FilteringSettings);

                // Render objects that did not match any shader pass with error shader
                RenderingUtils.RenderObjectsWithError(context, ref renderingData.cullResults, camera, m_FilteringSettings, SortingCriteria.None);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
