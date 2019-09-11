using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.LWRP;

internal class FeedBackCameraRender : ForwardRenderer
{

    public FeedbackPass m_feedbackPass;

    public FeedBackCameraRender(ForwardRendererData data) : base(data)
    {
        m_feedbackPass = new FeedbackPass(RenderPassEvent.BeforeRenderingOpaques, RenderQueueRange.opaque, data.opaqueLayerMask);
    }

    public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;
        RenderTextureDescriptor cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;

     
        // Depth prepass is generated in the following cases:
        // - We resolve shadows in screen space
        // - Scene view camera always requires a depth texture. We do a depth pre-pass to simplify it and it shouldn't matter much for editor.
        // - If game or offscreen camera requires it we check if we can copy the depth from the rendering opaques pass and use that instead.
        bool createColorTexture = false;
        // If camera requires depth and there's no depth pre-pass we create a depth texture that can be read
        // later by effect requiring it.
        bool createDepthTexture = renderingData.cameraData.requiresDepthTexture ;


        m_ActiveCameraColorAttachment =  RenderTargetHandle.CameraTarget;
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
        EnqueuePass(m_feedbackPass);

     
       
       

#if UNITY_EDITOR
        if (renderingData.cameraData.isSceneViewCamera)
        {
            m_SceneViewDepthCopyPass.Setup(m_DepthTexture);
            EnqueuePass(m_SceneViewDepthCopyPass);
        }
#endif
    }

}
