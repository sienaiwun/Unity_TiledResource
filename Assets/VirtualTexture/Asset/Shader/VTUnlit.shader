Shader "VirtualTexture/Unlit"
{
    SubShader
    {
		Tags{ "VirtualTextureType" = "Normal" }
        LOD 100

		  Pass
		{
		//Tags{"LightMode" = "LightweightForward"}
			CGPROGRAM
			#include "VTShading.cginc"	
			#pragma vertex VTVert
			#pragma fragment VTFragUnlit
			ENDCG
		}
        Pass
        {
			Tags{"LightMode" = "LightweightForward"}
            CGPROGRAM
			#include "VTShading.cginc"	
            #pragma vertex VTVert
            #pragma fragment VTFragUnlit
            ENDCG
        }
		Pass
		{
			Tags{"LightMode" = "FeedBackShader"}
			CGPROGRAM
			#include "VTFeedback.cginc"	
			#pragma vertex VTVert
			#pragma fragment VTFragFeedback
			ENDCG
		}
    }
}
