using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.LWRP;

[CreateAssetMenu(fileName = "ReflectionCamera Renderer", menuName = "Rendering/Lightweight Render Pipeline/ReflectionCamera Renderer", order = CoreUtils.assetCreateMenuPriority1)]
public class ReflectionCameraRenderData : ForwardRendererData
{
    protected override ScriptableRenderer Create()
    {
        return new ReflectionCameraRenderer(this);
    }
}
