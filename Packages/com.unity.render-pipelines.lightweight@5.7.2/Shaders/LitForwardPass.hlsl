#ifndef LIGHTWEIGHT_FORWARD_LIT_PASS2_INCLUDED
#define LIGHTWEIGHT_FORWARD_LIT_PASS2_INCLUDED

#include "SH.hlsl"
#include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/Lighting.hlsl"

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    float2 lightmapUV   : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv                       : TEXCOORD0;
    DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 1);
    float2 uvLM                     : TEXCOORD8;

#ifdef _ADDITIONAL_LIGHTS
    float3 positionWS               : TEXCOORD2;
#endif

#ifdef _NORMALMAP
    half4 normalWS                  : TEXCOORD3;    // xyz: normal, w: viewDir.x
    half4 tangentWS                 : TEXCOORD4;    // xyz: tangent, w: viewDir.y
    half4 bitangentWS                : TEXCOORD5;    // xyz: bitangent, w: viewDir.z
#else
    half3 normalWS                  : TEXCOORD3;
    half3 viewDirWS                 : TEXCOORD4;
#endif

    half4 fogFactorAndVertexLight   : TEXCOORD6; // x: fogFactor, yzw: vertex light

#ifdef _MAIN_LIGHT_SHADOWS
    float4 shadowCoord              : TEXCOORD7;
#endif

#if defined(PLANER_REFLECTION)
	float4 positionCS_TO_PS       : TEXCOORD9;
#endif

    float4 positionCS               : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
{
	inputData = (InputData)0;

#ifdef _ADDITIONAL_LIGHTS
	inputData.positionWS = input.positionWS;
#endif

#ifdef _NORMALMAP
	half3 viewDirWS = half3(input.normalWS.w, input.tangentWS.w, input.bitangentWS.w);
	inputData.normalWS = TransformTangentToWorld(normalTS,
		half3x3(input.tangentWS.xyz, input.bitangentWS.xyz, input.normalWS.xyz));
#else
	half3 viewDirWS = input.viewDirWS;
	inputData.normalWS = input.normalWS;
#endif

	inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
	viewDirWS = SafeNormalize(viewDirWS);

	inputData.viewDirectionWS = viewDirWS;
#if defined(_MAIN_LIGHT_SHADOWS) && !defined(_RECEIVE_SHADOWS_OFF)
	inputData.shadowCoord = input.shadowCoord;
#else
	inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
	inputData.fogCoord = input.fogFactorAndVertexLight.x;
	inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
	//inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, inputData.normalWS);



#ifdef LIGHTMAP_ON
	inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, inputData.normalWS);
#else
#ifdef INTERPOLATE_SH
	inputData.bakedGI = SampleSHs(inputData.normalWS, inputData.positionWS);
#else
	inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, inputData.normalWS);
#endif // INTERPOLATE_SH
#endif // LIGHTMAP_ON

#if defined(LIGHTMAP_ON) && defined(SHADOWS_SHADOWMASK)  
	inputData.bakedAtten = SampleShadowMask(input.lightmapUV);
#endif
#if defined(PLANER_REFLECTION)
	//screenUV.xyz /= screenUV.w;
    float4 temp_pos = input.positionCS_TO_PS;
     float4 screenUV = ComputeScreenPos(temp_pos);
                screenUV.xyz /= screenUV.w;
	inputData.screenPos =screenUV.xy ;
#endif
}

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

// Used in Standard (Physically Based) shader
Varyings LitPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    half3 viewDirWS = GetCameraPositionWS() - vertexInput.positionWS;
    half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
    half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);

#ifdef _NORMALMAP
    output.normalWS = half4(normalInput.normalWS, viewDirWS.x);
    output.tangentWS = half4(normalInput.tangentWS, viewDirWS.y);
    output.bitangentWS = half4(normalInput.bitangentWS, viewDirWS.z);
#else
    output.normalWS = NormalizeNormalPerVertex(normalInput.normalWS);
    output.viewDirWS = viewDirWS;
#endif

    OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
    OUTPUT_SH(output.normalWS.xyz, output.vertexSH);

    output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);

    // https://gist.github.com/phi-lira/225cd7c5e8545be602dca4eb5ed111ba
    output.uvLM = input.lightmapUV.xy * unity_LightmapST.xy + unity_LightmapST.zw;

#ifdef _ADDITIONAL_LIGHTS
    output.positionWS = vertexInput.positionWS;
#endif


	output.positionCS = vertexInput.positionCS;

#if defined(_MAIN_LIGHT_SHADOWS) && !defined(_RECEIVE_SHADOWS_OFF)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif

#if defined(PLANER_REFLECTION)
	output.positionCS_TO_PS =  vertexInput.positionCS;
#endif


    return output;
}


// Used in Standard (Physically Based) shader
half4 LitPassFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    SurfaceData surfaceData;
    float2 uv2 = input.uvLM;

#ifdef _MULTI_UV
    // Check for invalid lightmap uv values
    if (abs(uv2.x - _uvLMZero) + abs(uv2.y - _uvLMZero) > _uvLMEpsilon)
    {
        InitializeStandardLitSurfaceData2(input.uv, uv2, surfaceData);
    }
    else
    {
        InitializeStandardLitSurfaceData(input.uv, surfaceData);
    }
#else
    InitializeStandardLitSurfaceData(input.uv, surfaceData);
#endif

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);

    half4 color = LightweightFragmentPBR(inputData, surfaceData.albedo, surfaceData.metallic, surfaceData.specular, surfaceData.smoothness, surfaceData.occlusion, surfaceData.emission, surfaceData.alpha);

    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    return color;
}

#endif
