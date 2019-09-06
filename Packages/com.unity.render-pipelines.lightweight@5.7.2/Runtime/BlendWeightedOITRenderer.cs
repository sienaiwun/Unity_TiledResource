using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.LWRP;

internal class BlendWeightedOITRenderer : ForwardRenderer
{
    BlendWeightedAccumulatePass m_BlendWeightedAccumulatePass;
    BlendWeightedRevealagePass m_BlendWeightedRevealagePass;
    //BlendWeightedTestPass m_BlendWeightedTestPass;

    RenderTargetHandle accumulateHandle;
    RenderTargetHandle revealageHandle;

    public BlendWeightedOITRenderer(ForwardRendererData data) : base(data)
    {
        m_BlendWeightedAccumulatePass = new BlendWeightedAccumulatePass(RenderPassEvent.BeforeRenderingTransparents, RenderQueueRange.transparent, data.transparentLayerMask);
        m_BlendWeightedRevealagePass = new BlendWeightedRevealagePass(RenderPassEvent.BeforeRenderingTransparents, RenderQueueRange.transparent, data.transparentLayerMask);
        //m_BlendWeightedTestPass = new BlendWeightedTestPass(RenderPassEvent.BeforeRenderingTransparents, RenderQueueRange.transparent, data.transparentLayerMask);
        accumulateHandle.Init("_AccumTex");
        revealageHandle.Init("_RevealageTex");

    }

    public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;
        RenderTextureDescriptor cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;

        bool mainLightShadows = m_MainLightShadowCasterPass.Setup(ref renderingData);
        bool additionalLightShadows = m_AdditionalLightsShadowCasterPass.Setup(ref renderingData);
        bool resolveShadowsInScreenSpace = mainLightShadows && renderingData.shadowData.requiresScreenSpaceShadowResolve;

        // Depth prepass is generated in the following cases:
        // - We resolve shadows in screen space
        // - Scene view camera always requires a depth texture. We do a depth pre-pass to simplify it and it shouldn't matter much for editor.
        // - If game or offscreen camera requires it we check if we can copy the depth from the rendering opaques pass and use that instead.
        bool requiresDepthPrepass = renderingData.cameraData.isSceneViewCamera ||
            (renderingData.cameraData.requiresDepthTexture && (!CanCopyDepth(ref renderingData.cameraData)));
        requiresDepthPrepass |= resolveShadowsInScreenSpace;
        bool createColorTexture = RequiresIntermediateColorTexture(ref renderingData, cameraTargetDescriptor)
                                  || rendererFeatures.Count != 0;

        // If camera requires depth and there's no depth pre-pass we create a depth texture that can be read
        // later by effect requiring it.
        bool createDepthTexture = renderingData.cameraData.requiresDepthTexture && !requiresDepthPrepass;
        bool postProcessEnabled = renderingData.cameraData.postProcessEnabled;
        bool hasOpaquePostProcess = postProcessEnabled &&
            renderingData.cameraData.postProcessLayer.HasOpaqueOnlyEffects(RenderingUtils.postProcessRenderContext);

        m_ActiveCameraColorAttachment = (createColorTexture) ? m_CameraColorAttachment : RenderTargetHandle.CameraTarget;
        m_ActiveCameraDepthAttachment = (createDepthTexture) ? m_CameraDepthAttachment : RenderTargetHandle.CameraTarget;
        if (createColorTexture || createDepthTexture)
            CreateCameraRenderTarget(context, ref renderingData.cameraData);
        ConfigureCameraTarget(m_ActiveCameraColorAttachment.Identifier(), m_ActiveCameraDepthAttachment.Identifier());

        for (int i = 0; i < rendererFeatures.Count; ++i)
        {
            rendererFeatures[i].AddRenderPasses(this, ref renderingData);
        }

        int count = activeRenderPassQueue.Count;
        for (int i = count - 1; i >= 0; i--)
        {
            if (activeRenderPassQueue[i] == null)
                activeRenderPassQueue.RemoveAt(i);
        }
        bool hasAfterRendering = activeRenderPassQueue.Find(x => x.renderPassEvent == RenderPassEvent.AfterRendering) != null;

        if (mainLightShadows)
            EnqueuePass(m_MainLightShadowCasterPass);

        if (additionalLightShadows)
            EnqueuePass(m_AdditionalLightsShadowCasterPass);

        if (requiresDepthPrepass)
        {
            m_DepthPrepass.Setup(cameraTargetDescriptor, m_DepthTexture);
            EnqueuePass(m_DepthPrepass);
        }

        if (resolveShadowsInScreenSpace)
        {
            m_ScreenSpaceShadowResolvePass.Setup(cameraTargetDescriptor);
            EnqueuePass(m_ScreenSpaceShadowResolvePass);
            if (renderingData.shadowData.ssShadowDownSampleSize > 1)
            {
                m_SSSDownsamplePass.Setup(cameraTargetDescriptor, renderingData);
                EnqueuePass(m_SSSDownsamplePass);
            }
        }

        EnqueuePass(m_RenderOpaqueForwardPass);

        if (hasOpaquePostProcess)
            m_OpaquePostProcessPass.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment, m_ActiveCameraColorAttachment);

        //EnqueuePass(m_RenderOpaqueDiscardAndBlendPass);

        if (camera.clearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null)
            EnqueuePass(m_DrawSkyboxPass);

        // If a depth texture was created we necessarily need to copy it, otherwise we could have render it to a renderbuffer
        if (createDepthTexture)
        {
            m_CopyDepthPass.Setup(m_ActiveCameraDepthAttachment, m_DepthTexture);
            EnqueuePass(m_CopyDepthPass);
        }

        if (renderingData.cameraData.requiresOpaqueTexture)
        {
            m_CopyColorPass.Setup(m_ActiveCameraColorAttachment.Identifier(), m_OpaqueColor);
            EnqueuePass(m_CopyColorPass);
        }

        //OIT Pass
        m_BlendWeightedAccumulatePass.Setup(cameraTargetDescriptor, accumulateHandle, m_DepthTexture);
        //m_BlendWeightedAccumulatePass.Setup(new RenderTextureDescriptor(cameraTargetDescriptor.width, cameraTargetDescriptor.height), accumulateHandle, m_DepthTexture);
        EnqueuePass(m_BlendWeightedAccumulatePass);
        m_BlendWeightedRevealagePass.Setup(cameraTargetDescriptor, revealageHandle, m_DepthTexture);
        //m_BlendWeightedRevealagePass.Setup(new RenderTextureDescriptor(cameraTargetDescriptor.width, cameraTargetDescriptor.height), revealageHandle, m_DepthTexture);
        EnqueuePass(m_BlendWeightedRevealagePass);

        //cameraTargetDescriptor.msaaSamples = msaaSamples;
        //cameraTargetDescriptor.bindMS = bindMS;
        //Blit Feature  AddPass(EnqueuePass


        bool afterRenderExists = renderingData.cameraData.captureActions != null ||
                                 hasAfterRendering;

        // if we have additional filters
        // we need to stay in a RT
        if (afterRenderExists)
        {
            // perform post with src / dest the same
            if (postProcessEnabled)
            {
                m_PostProcessPass.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment, m_ActiveCameraColorAttachment);
                EnqueuePass(m_PostProcessPass);
            }

            //now blit into the final target
            if (m_ActiveCameraColorAttachment != RenderTargetHandle.CameraTarget)
            {
                if (renderingData.cameraData.captureActions != null)
                {
                    m_CapturePass.Setup(m_ActiveCameraColorAttachment);
                    EnqueuePass(m_CapturePass);
                }

                m_FinalBlitPass.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment);
                EnqueuePass(m_FinalBlitPass);
            }
        }
        else
        {
            if (postProcessEnabled)
            {
                m_PostProcessPass.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment, RenderTargetHandle.CameraTarget);
                EnqueuePass(m_PostProcessPass);
            }
            else if (m_ActiveCameraColorAttachment != RenderTargetHandle.CameraTarget)
            {
                m_FinalBlitPass.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment);
                EnqueuePass(m_FinalBlitPass);
            }
        }

#if UNITY_EDITOR
        if (renderingData.cameraData.isSceneViewCamera)
        {
            m_SceneViewDepthCopyPass.Setup(m_DepthTexture);
            EnqueuePass(m_SceneViewDepthCopyPass);
        }
#endif
    }

}
