using System.Collections.Generic;

namespace UnityEngine.Rendering.LWRP
{
    /// <summary>
    /// Copy the given color target to the current camera target
    ///
    /// You can use this pass to copy the result of rendering to
    /// the camera target. The pass takes the screen viewport into
    /// consideration.
    /// </summary>
    internal class UICameraPass : ScriptableRenderPass
    {
        FilteringSettings m_FilteringSettings;
        const string m_ProfilerTag = "UICameraPass";
        List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>();
        RenderTargetHandle m_RenderTargetHandle;

        public UICameraPass(RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask)
        {
            m_ShaderTagIdList.Add(new ShaderTagId("LightweightForward"));
            m_ShaderTagIdList.Add(new ShaderTagId("SRPDefaultUnlit"));
            renderPassEvent = evt;

            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
        }

        public void Setup(RenderTargetHandle rtHandle)
        {
            m_RenderTargetHandle = rtHandle;
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
            using (new ProfilingSample(cmd, m_ProfilerTag))
            {
                
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                SetRenderTarget(
                    cmd,
                    m_RenderTargetHandle.Identifier(),
                    RenderBufferLoadAction.Load,
                    RenderBufferStoreAction.Store,
                    ClearFlag.Depth,
                    Color.black,
                    TargetDimension);
                

                
                // GetCameraDescriptor
                Camera uiCamera = renderingData.uiCmaera;

                Matrix4x4 projMatrix = uiCamera.projectionMatrix;
                Matrix4x4 viewMatrix = uiCamera.worldToCameraMatrix;
                //Matrix4x4 viewProjMatrix = projMatrix * viewMatrix;
                //cmd.SetGlobalMatrix("UNITY_MATRIX_VP", viewProjMatrix);
                cmd.SetProjectionMatrix(projMatrix);
                cmd.SetViewMatrix(viewMatrix);
                cmd.SetViewProjectionMatrices(viewMatrix, projMatrix);
                cmd.SetViewport(uiCamera.pixelRect);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                LWRPAdditionalCameraData additionalCameraData = null;
                //LightweightRenderPipeline.BeginCameraRenderingWrapper(context, uiCamera);
                if (!uiCamera.TryGetCullingParameters(LightweightRenderPipeline.IsStereoEnabled(uiCamera), out var cullingParameters))
                    return;
                if (uiCamera.cameraType == CameraType.Game || uiCamera.cameraType == CameraType.VR)
                {
                    additionalCameraData = uiCamera.gameObject.GetComponent<LWRPAdditionalCameraData>();
                }
                LightweightRenderPipeline.InitializeCameraData(renderingData.pipelineAsset, uiCamera, additionalCameraData, out var uiCameraData);
                LightweightRenderPipeline.SetupPerCameraShaderConstants(uiCameraData);

                
                // Get Rendering data
                
                var cullResults = context.Cull(ref cullingParameters);
                LightweightRenderPipeline.InitializeRenderingData(renderingData.pipelineAsset, ref uiCameraData, ref cullResults, out var uiRenderingData);

                //var drawOpaqueSettings = CreateDrawingSettings(m_ShaderTagIdList, ref uiRenderingData, SortingCriteria.CommonOpaque);
                //context.DrawRenderers(uiRenderingData.cullResults, ref drawOpaqueSettings, ref m_FilteringSettings);

                var drawTransparentSettings = CreateDrawingSettings(m_ShaderTagIdList, ref uiRenderingData, SortingCriteria.CommonTransparent);
                context.DrawRenderers(uiRenderingData.cullResults, ref drawTransparentSettings, ref m_FilteringSettings);
                
                // Render objects that did not match any shader pass with error shader
                RenderingUtils.RenderObjectsWithError(context, ref uiRenderingData.cullResults, uiCamera, m_FilteringSettings, SortingCriteria.None);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
