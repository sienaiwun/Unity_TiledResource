using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.LWRP;
using VirtualTexture;


[ImageEffectAllowedInSceneView]
public class FeedBackCamera : MonoBehaviour, IBeforeCameraRender
{
    const string k_feedback_process = "FeedBack Rendering Process";

    private RenderTexture m_FeedBackTexture = null;
    private Camera m_FeedBackCamera = null;
    private int m_TextureSize = 256;
    public FrameStat Stat { get; private set; } = new FrameStat();

    private void CreateFeedbackTextureIfNone(Camera cam)
    {
        LightweightRenderPipelineAsset lwAsset = (LightweightRenderPipelineAsset)GraphicsSettings.renderPipelineAsset;
        m_TextureSize = (int)Mathf.Pow(2, Mathf.RoundToInt(Mathf.Log(cam.pixelWidth, 2)));
        if (!m_FeedBackTexture)
        {
            if (m_FeedBackTexture)
                DestroyImmediate(m_FeedBackTexture);
            m_FeedBackTexture = new RenderTexture(m_TextureSize, m_TextureSize, 16, RenderTextureFormat.Default);
            m_FeedBackTexture.autoGenerateMips = false; // no need for mips(unless wanting cheap roughness)
            m_FeedBackTexture.name = "_FeedbackTexture" + GetInstanceID();
            m_FeedBackTexture.isPowerOfTwo = true;
            m_FeedBackTexture.hideFlags = HideFlags.DontSave;
            m_FeedBackTexture.filterMode = FilterMode.Trilinear;
        }
        m_FeedBackTexture.DiscardContents();
    }

    private Camera CreateFeedBackCamera(Camera currentCamera)
    {
        GameObject go =
           new GameObject("Feedback Camera id" + GetInstanceID() + " for " + currentCamera.GetInstanceID(),
               typeof(Camera));
        var feedBackCamera = go.GetComponent<Camera>();
        feedBackCamera.transform.SetPositionAndRotation(transform.position, transform.rotation);
        feedBackCamera.targetTexture = m_FeedBackTexture;
        feedBackCamera.allowMSAA = false;
        feedBackCamera.depth = -10;
        feedBackCamera.name = FeedbackGlobals.FeedbackCamName;
        feedBackCamera.allowHDR = false;
        feedBackCamera.renderingPath = RenderingPath.Forward;
        feedBackCamera.clearFlags = CameraClearFlags.Color;
        feedBackCamera.backgroundColor = Color.white;
        go.hideFlags = HideFlags.DontSave;
        return feedBackCamera;
    }

    private void UpdateCamera(Camera src, Camera dest)
    {
        if (dest == null)
            return;
        dest.clearFlags = src.clearFlags;
        dest.backgroundColor = src.backgroundColor;
        // update other values to match current camera.
        // even if we are supplying custom camera&projection matrices,
        // some of values are used elsewhere (e.g. skybox uses far plane)
        dest.farClipPlane = src.farClipPlane;
        dest.nearClipPlane = src.nearClipPlane;
        dest.orthographic = src.orthographic;
        dest.fieldOfView = src.fieldOfView;
        dest.transform.position = src.transform.position;
        dest.transform.rotation = src.transform.rotation;
        dest.cullingMask = src.cullingMask;
        dest.projectionMatrix = src.projectionMatrix;
        dest.allowHDR = src.allowHDR;
        dest.useOcclusionCulling = false;
        dest.aspect = src.aspect;
        dest.orthographicSize = src.orthographicSize;
    }

    private void CreateFeedbackCameraIfNone(Camera cam)
    {
        if (m_FeedBackCamera == null)
            m_FeedBackCamera = CreateFeedBackCamera(cam);
        UpdateCamera(cam, m_FeedBackCamera);
    }


    void IBeforeCameraRender.ExecuteBeforeCameraRender(LightweightRenderPipeline pipelineInstance, ScriptableRenderContext context, Camera camera)
    {
        if (!enabled)
            return;
        Stat.BeginFrame();
        CreateFeedbackTextureIfNone(camera);
        CreateFeedbackCameraIfNone(camera);
        LightweightRenderPipeline.RenderSingleCamera(context, m_FeedBackCamera);
        Stat.EndFrame();

    }

    void OnDisable()
    {
        if (m_FeedBackCamera)
        {
            m_FeedBackCamera.targetTexture = null;
            DestroyImmediate(m_FeedBackCamera.gameObject);
        }
        if (m_FeedBackTexture)
        {
            DestroyImmediate(m_FeedBackTexture);
        }
    }
}
