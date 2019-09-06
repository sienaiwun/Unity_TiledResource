using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.LWRP;

[CreateAssetMenu(fileName = "OIT Renderer", menuName = "Rendering/Lightweight Render Pipeline/OIT Renderer", order = CoreUtils.assetCreateMenuPriority1)]
public class BlendWeightedOITRenderData : ForwardRendererData
{
    protected override ScriptableRenderer Create()
    {
        return new BlendWeightedOITRenderer(this);
    }
}
