using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.LWRP;

public enum ScaleFactor
{
    One,

    Half,

    Quarter,

    Eighth,

}

public static class ScaleModeExtensions
{
    public static float ToFloat(this ScaleFactor mode)
    {
        switch (mode)
        {
            case ScaleFactor.Eighth:
                return 0.125f;
            case ScaleFactor.Quarter:
                return 0.25f;
            case ScaleFactor.Half:
                return 0.5f;
        }
        return 1;
    }
}

[ImageEffectAllowedInSceneView]
public class FeedBackCamera : MonoBehaviour, IBeforeCameraRender
{
    private Queue<AsyncGPUReadbackRequest> m_ReadbackRequests = new Queue<AsyncGPUReadbackRequest>();

    const string k_Reflection_process = "Reflection Blur PostProcess";
    const string k_glossy_enable = "_GLOSSY_REFLECTION";
    
    private Camera m_CamputureCamera;
    public RenderTexture m_CamptureTexture = null;
    public RenderTexture m_DownSampleTexture = null;
    public Texture2D m_ReadbackTexture;
    public event Action<Texture2D> readTextureAction;

    [SerializeField]
    private ScaleFactor m_Scale = default;
    private Material m_DownScaleMaterial;
    private int m_DownScaleMaterialPass;
    [SerializeField]
    private Shader m_DownScaleShader = default;
    // Cleanup all the objects we possibly have created
    void OnDisable()
    {
        if (m_CamputureCamera)
        {
            m_CamputureCamera.targetTexture = null;
            DestroyImmediate(m_CamputureCamera.gameObject);
        }
        if (m_CamptureTexture)
        {
            DestroyImmediate(m_CamptureTexture);
        }
    }

    private void InitMaterial()
    {
        if (m_Scale != ScaleFactor.One)
        {
            if(m_DownScaleMaterial == null)
                m_DownScaleMaterial = new Material(m_DownScaleShader);

            switch (m_Scale)
            {
                case ScaleFactor.Half:
                    m_DownScaleMaterialPass = 0;
                    break;
                case ScaleFactor.Quarter:
                    m_DownScaleMaterialPass = 1;
                    break;
                case ScaleFactor.Eighth:
                    m_DownScaleMaterialPass = 2;
                    break;
            }
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
        if (m_CamputureCamera == null)
            m_CamputureCamera = CreateMirrorObjects(realCamera);
        UpdateCamera(realCamera, m_CamputureCamera);

     
      

        m_CamputureCamera.transform.forward = realCamera.transform.forward;
        m_CamputureCamera.transform.rotation = realCamera.transform.rotation;
        m_CamputureCamera.transform.position = realCamera.transform.position;
        m_CamputureCamera.worldToCameraMatrix = realCamera.worldToCameraMatrix ;
        m_CamputureCamera.renderingPath = RenderingPath.Forward;
        m_CamputureCamera.projectionMatrix = realCamera.projectionMatrix;
        m_CamputureCamera.clearFlags = CameraClearFlags.Color;
        m_CamputureCamera.backgroundColor = Color.white;
        m_CamputureCamera.depthTextureMode = DepthTextureMode.Depth;
        m_CamputureCamera.useOcclusionCulling = false;

    }
    private void CreateTextureIfNone(Camera currentCamera)
    {
        LightweightRenderPipelineAsset lwAsset = (LightweightRenderPipelineAsset)GraphicsSettings.renderPipelineAsset;
        //m_TextureSize.y = (int)Mathf.Pow(2, Mathf.RoundToInt(Mathf.Log(currentCamera.pixelHeight * resMulti, 2)));

        // Reflection render texture
        //if (Int2Compare(m_TextureSize, m_OldReflectionTextureSize) || !m_ReflectionTexture)
        if (!m_CamptureTexture )
        {
            if (m_CamptureTexture)
                DestroyImmediate(m_CamptureTexture);
            

            m_CamptureTexture = new RenderTexture(currentCamera.pixelWidth, currentCamera.pixelHeight, 16, RenderTextureFormat.Default);
            m_CamptureTexture.useMipMap = m_CamptureTexture.autoGenerateMips = false;
            m_CamptureTexture.autoGenerateMips = false; // no need for mips(unless wanting cheap roughness)
            m_CamptureTexture.name = "_PlanarReflection" + GetInstanceID();
            m_CamptureTexture.hideFlags = HideFlags.DontSave;
            m_CamptureTexture.filterMode = FilterMode.Point;
            
        }
        m_CamptureTexture.DiscardContents();

    }

    private Camera CreateMirrorObjects(Camera currentCamera)
    {
        GameObject go =
            new GameObject("Feedback Camera" + GetInstanceID() + " for " + currentCamera.GetInstanceID(),
                typeof(Camera));
        var feedBackCamera = go.GetComponent<Camera>();
        feedBackCamera.transform.SetPositionAndRotation(transform.position, transform.rotation);
        feedBackCamera.targetTexture = m_CamptureTexture;
        feedBackCamera.allowMSAA = false;
        feedBackCamera.depth = -10;
        feedBackCamera.enabled = false;
        feedBackCamera.name = FeedbackGlobals.FeedbackCamName;
        feedBackCamera.allowHDR = false;

        go.hideFlags = HideFlags.HideAndDontSave;
        return feedBackCamera;
    }


    public void ExecuteBeforeCameraRender(
        LightweightRenderPipeline pipelineInstance,
        ScriptableRenderContext context,
        Camera camera)
    {

        if (!enabled)
            return;
        InitMaterial();
        UpdateReflectionCamera(camera);
        LightweightRenderPipeline.RenderSingleCamera(context, m_CamputureCamera);

        NewRequest();
       
    }

    private void NewRequest()
    {
        if (m_ReadbackRequests.Count > 8)
            return;
        int width = (int)(m_CamptureTexture.width *m_Scale.ToFloat());
        int height = (int)(m_CamptureTexture.height * m_Scale.ToFloat());
        if (m_Scale != ScaleFactor.One)
        {
            if (m_DownSampleTexture == null || m_DownSampleTexture.width != width || m_DownSampleTexture.height != height)
            {
                m_DownSampleTexture = new RenderTexture(width, height, 0);
            }
            m_DownSampleTexture.DiscardContents();
            Graphics.Blit(m_CamptureTexture, m_DownSampleTexture, m_DownScaleMaterial, m_DownScaleMaterialPass);
        }
        if (m_ReadbackTexture == null || m_ReadbackTexture.width != width || m_ReadbackTexture.height != height)
        {
            m_ReadbackTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            m_ReadbackTexture.filterMode = FilterMode.Point;
            m_ReadbackTexture.wrapMode = TextureWrapMode.Clamp;

        }
        AsyncGPUReadbackRequest request;
        if (m_Scale != ScaleFactor.One)
        {

             request = AsyncGPUReadback.Request(m_DownSampleTexture);
        }
        else
        {

             request = AsyncGPUReadback.Request(m_CamptureTexture);
        }


        m_ReadbackRequests.Enqueue(request);
    }

    private void UpdateRequest()
    {
        bool complete = false;
        while (m_ReadbackRequests.Count > 0)
        {
            var req = m_ReadbackRequests.Peek();

            if (req.hasError)
            {
               // ReadbackStat.EndRequest(req, false);
                m_ReadbackRequests.Dequeue();
            }
            else if (req.done)
            {
                // 更新数据并分发事件
                m_ReadbackTexture.GetRawTextureData<Color32>().CopyFrom(req.GetData<Color32>());
                m_ReadbackTexture.Apply();
                complete = true;

             //   ReadbackStat.EndRequest(req, true);
                m_ReadbackRequests.Dequeue();
            }
            else
            {
                break;
            }
        }

        if (complete)
        {
            readTextureAction?.Invoke(m_ReadbackTexture);
        }
    }


    void Update()
    {
         UpdateRequest();
    }


}
