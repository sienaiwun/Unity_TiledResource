using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.LWRP;
using VirtualTexture;

[ImageEffectAllowedInSceneView]
public class FeedBackCamera : MonoBehaviour, IBeforeCameraRender
{

    const string k_Reflection_process = "Reflection Blur PostProcess";
    const string k_glossy_enable = "_GLOSSY_REFLECTION";
    
    
    public Material m_matCopyDepth = null;
    public Material m_matReflectionBlur = null;
    private Camera m_ReflectionCamera;
    private RenderTexture m_ReflectionTexture = null;

    private Vector4 reflectionPlane;

    public FrameStat Stat { get; private set; } = new FrameStat();

    // Cleanup all the objects we possibly have created
    void OnDisable()
    {
        if (m_ReflectionCamera)
        {
            m_ReflectionCamera.targetTexture = null;
            DestroyImmediate(m_ReflectionCamera.gameObject);
        }
        if (m_ReflectionTexture)
        {
            DestroyImmediate(m_ReflectionTexture);
        }
    }

    private void UpdateCamera(Camera src, Camera dest)
    {
        if (dest == null)
            return;
        // set camera to clear the same way as current camera
        dest.clearFlags = src.clearFlags;
        dest.backgroundColor = src.backgroundColor;
        // update other values to match current camera.
        // even if we are supplying custom camera&projection matrices,
        // some of values are used elsewhere (e.g. skybox uses far plane)
        dest.farClipPlane = src.farClipPlane;
        dest.nearClipPlane = src.nearClipPlane;
        dest.orthographic = src.orthographic;
        dest.fieldOfView = src.fieldOfView;
        dest.allowHDR = src.allowHDR;
        dest.useOcclusionCulling = false;
        dest.aspect = src.aspect;
        dest.orthographicSize = src.orthographicSize;
    }


    private void UpdateReflectionCamera(Camera realCamera)
    {
        CreateTextureIfNone(realCamera);
        if (m_ReflectionCamera == null)
            m_ReflectionCamera = CreateMirrorObjects(realCamera);
        UpdateCamera(realCamera, m_ReflectionCamera);

     
      

        m_ReflectionCamera.transform.forward = realCamera.transform.forward;
        m_ReflectionCamera.transform.rotation = realCamera.transform.rotation;
        m_ReflectionCamera.transform.position = realCamera.transform.position;
        m_ReflectionCamera.worldToCameraMatrix = realCamera.worldToCameraMatrix ;
        m_ReflectionCamera.renderingPath = RenderingPath.Forward;
        m_ReflectionCamera.projectionMatrix = realCamera.projectionMatrix;
        m_ReflectionCamera.clearFlags = CameraClearFlags.Color;
        m_ReflectionCamera.backgroundColor = Color.white;
        m_ReflectionCamera.depthTextureMode = DepthTextureMode.Depth;
        m_ReflectionCamera.useOcclusionCulling = false;

    }
    private void CreateTextureIfNone(Camera currentCamera)
    {
        LightweightRenderPipelineAsset lwAsset = (LightweightRenderPipelineAsset)GraphicsSettings.renderPipelineAsset;
        //m_TextureSize.y = (int)Mathf.Pow(2, Mathf.RoundToInt(Mathf.Log(currentCamera.pixelHeight * resMulti, 2)));

        // Reflection render texture
        //if (Int2Compare(m_TextureSize, m_OldReflectionTextureSize) || !m_ReflectionTexture)
        if (!m_ReflectionTexture )
        {
            if (m_ReflectionTexture)
                DestroyImmediate(m_ReflectionTexture);
            

            m_ReflectionTexture = new RenderTexture(currentCamera.pixelWidth, currentCamera.pixelHeight, 16, RenderTextureFormat.Default);
            m_ReflectionTexture.useMipMap = m_ReflectionTexture.autoGenerateMips = true;
            m_ReflectionTexture.autoGenerateMips = true; // no need for mips(unless wanting cheap roughness)
            m_ReflectionTexture.name = "_PlanarReflection" + GetInstanceID();
            m_ReflectionTexture.hideFlags = HideFlags.DontSave;
            m_ReflectionTexture.filterMode = FilterMode.Trilinear;
            
        }
        m_ReflectionTexture.DiscardContents();

    }

    private Camera CreateMirrorObjects(Camera currentCamera)
    {
        GameObject go =
            new GameObject("Planar Refl Camera id" + GetInstanceID() + " for " + currentCamera.GetInstanceID(),
                typeof(Camera), typeof(Skybox));
        var reflectionCamera = go.GetComponent<Camera>();
        reflectionCamera.transform.SetPositionAndRotation(transform.position, transform.rotation);
        reflectionCamera.targetTexture = m_ReflectionTexture;
        reflectionCamera.allowMSAA = true;
        reflectionCamera.depth = -10;
        reflectionCamera.enabled = false;
        reflectionCamera.name = FeedbackGlobals.FeedbackCamName;
        reflectionCamera.allowHDR = false;

        go.hideFlags = HideFlags.HideAndDontSave;
        return reflectionCamera;
    }


    public void ExecuteBeforeCameraRender(
        LightweightRenderPipeline pipelineInstance,
        ScriptableRenderContext context,
        Camera camera)
    {

        if (!enabled)
            return;
        UpdateReflectionCamera(camera);
        Stat.BeginFrame();
        LightweightRenderPipeline.RenderSingleCamera(context, m_ReflectionCamera);
        Stat.EndFrame();
       
    }


}
