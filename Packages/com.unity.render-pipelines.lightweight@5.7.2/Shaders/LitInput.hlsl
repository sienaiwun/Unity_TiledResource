#ifndef LIGHTWEIGHT_LIT_INPUT2_INCLUDED
#define LIGHTWEIGHT_LIT_INPUT2_INCLUDED

#include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
half4 _BaseColor;
half4 _SpecColor;
half4 _EmissionColor;
half _Cutoff;
half _Smoothness;
half _Metallic;
half _BumpScale;
half _OcclusionStrength;
CBUFFER_END

TEXTURE2D(_dayCycle_Lightmap);
TEXTURE2D(_OcclusionMap);       SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);
TEXTURE2D(_SpecGlossMap);       SAMPLER(sampler_SpecGlossMap);


#ifdef _MULTI_UV
float _uvLMZero;
float _uvLMEpsilon;

#ifdef _TEXTURE_BLENDING
TEXTURE2D(_BaseMap2);            SAMPLER(sampler_BaseMap2);
TEXTURE2D(_BumpMap2);            SAMPLER(sampler_BumpMap2);
TEXTURE2D(_MetallicGlossMap2);   SAMPLER(sampler_MetallicGlossMap2);
float _TextureScale;
float _uvLMScale;
float _uvLMOffset;
#endif // _TEXTURE_BLENDING

#ifdef _PATTERN_BLENDING
TEXTURE2D(_BaseMap3);            SAMPLER(sampler_BaseMap3);
TEXTURE2D(_BumpMap3);            SAMPLER(sampler_BumpMap3);
TEXTURE2D(_MetallicGlossMap3);   SAMPLER(sampler_MetallicGlossMap3);
float _PatternScale;
float _Pattern_uvLMScale;
float _Pattern_uvLMOffset;
#endif // _PATTERN_BLENDING

#ifdef _NORMAL_NOISE
TEXTURE2D(_NoiseBumpMap);            SAMPLER(sampler_NoiseBumpMap);
float _NoiseNormalScale;
float _NoiseUVScale;
float _Noise_uvLMOffset;
#endif // _NORMAL_NOISE

#endif // _MULTI_UV

#ifdef _SPECULAR_SETUP
    #define SAMPLE_METALLICSPECULAR(uv) SAMPLE_TEXTURE2D(_SpecGlossMap, sampler_SpecGlossMap, uv)
#else
    #define SAMPLE_METALLICSPECULAR(uv) SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv)  
#endif

half4 SampleMetallicSpecGloss(float2 uv, half albedoAlpha)
{
    half4 specGloss;

#ifdef _METALLICSPECGLOSSMAP
    specGloss = SAMPLE_METALLICSPECULAR(uv);
    #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
        specGloss.a = albedoAlpha * _Smoothness;
    #else
        specGloss.a *= _Smoothness;
    #endif
#else // _METALLICSPECGLOSSMAP
    #if _SPECULAR_SETUP
        specGloss.rgb = _SpecColor.rgb;
    #else
        specGloss.rgb = _Metallic.rrr;
    #endif

    #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
        specGloss.a = albedoAlpha * _Smoothness;
    #else
        specGloss.a = _Smoothness;
    #endif
#endif

    return specGloss;
}

#ifdef _MULTI_UV

#ifdef _TEXTURE_BLENDING
#ifdef _SPECULAR_SETUP
    #define SAMPLE_METALLICSPECULAR2(uv) SAMPLE_TEXTURE2D(_SpecGlossMap, sampler_SpecGlossMap, uv)
#else
    #define SAMPLE_METALLICSPECULAR2(uv) SAMPLE_TEXTURE2D(_MetallicGlossMap2, sampler_MetallicGlossMap2, uv)
#endif

half4 SampleMetallicSpecGloss2(float2 uv, half albedoAlpha)
{
    half4 specGloss;

    #ifdef _METALLICSPECGLOSSMAP
        specGloss = SAMPLE_METALLICSPECULAR2(uv);
        #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            specGloss.a = albedoAlpha * _Smoothness;
        #else
            specGloss.a *= _Smoothness;
        #endif
    #else // _METALLICSPECGLOSSMAP
        #if _SPECULAR_SETUP
            specGloss.rgb = _SpecColor.rgb;
        #else
            specGloss.rgb = _Metallic.rrr;
        #endif

        #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            specGloss.a = albedoAlpha * _Smoothness;
        #else
            specGloss.a = _Smoothness;
        #endif
    #endif

    return specGloss;
}

#endif // _TEXTURE_BLENDING

#ifdef _PATTERN_BLENDING
#ifdef _SPECULAR_SETUP
    #define SAMPLE_METALLICSPECULAR3(uv) SAMPLE_TEXTURE2D(_SpecGlossMap, sampler_SpecGlossMap, uv)
#else
    #define SAMPLE_METALLICSPECULAR3(uv) SAMPLE_TEXTURE2D(_MetallicGlossMap3, sampler_MetallicGlossMap3, uv)    
#endif

half4 SampleMetallicSpecGloss3(float2 uv, half albedoAlpha)
{
    half4 specGloss;

    #ifdef _METALLICSPECGLOSSMAP
        specGloss = SAMPLE_METALLICSPECULAR3(uv);
        #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            specGloss.a = albedoAlpha * _Smoothness;
        #else
            specGloss.a *= _Smoothness;
        #endif
    #else // _METALLICSPECGLOSSMAP
        #if _SPECULAR_SETUP
            specGloss.rgb = _SpecColor.rgb;
        #else
            specGloss.rgb = _Metallic.rrr;
        #endif

        #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            specGloss.a = albedoAlpha * _Smoothness;
        #else
            specGloss.a = _Smoothness;
        #endif
    #endif

    return specGloss;
}

#endif // _PATTERN_BLENDING

#endif // _MULTI_UV

half SampleOcclusion(float2 uv)
{
#ifdef _OCCLUSIONMAP
// TODO: Controls things like these by exposing SHADER_QUALITY levels (low, medium, high)
#if defined(SHADER_API_GLES)
    return SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).g;
#else
    half occ = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).g;
    return LerpWhiteTo(occ, _OcclusionStrength);
#endif
#else
    return 1.0;
#endif
}

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    outSurfaceData.alpha = Alpha(albedoAlpha.a, _BaseColor, _Cutoff);

    half4 specGloss = SampleMetallicSpecGloss(uv, albedoAlpha.a);
    outSurfaceData.albedo = albedoAlpha.rgb * _BaseColor.rgb;

#if _SPECULAR_SETUP
    outSurfaceData.metallic = 1.0h;
    outSurfaceData.specular = specGloss.rgb;
#else
    outSurfaceData.metallic = specGloss.r;
    outSurfaceData.specular = half3(0.0h, 0.0h, 0.0h);
#endif

    outSurfaceData.smoothness = specGloss.a;
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    outSurfaceData.occlusion = SampleOcclusion(uv);
    outSurfaceData.emission = SampleEmission(uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));
}

#ifdef _MULTI_UV
inline void InitializeStandardLitSurfaceData2(float2 uv, float2 uvLM, out SurfaceData outSurfaceData)
{
#ifdef _TEXTURE_BLENDING
    float2 uv2 = uvLM * _uvLMScale + _uvLMOffset;
#endif
#ifdef _PATTERN_BLENDING
    float2 uv3 = uvLM * _Pattern_uvLMScale + _Pattern_uvLMOffset;
#endif
#ifdef _NORMAL_NOISE
    float2 uvNoise = uvLM * _NoiseUVScale + _Noise_uvLMOffset;
#endif

    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    #ifdef _TEXTURE_BLENDING
        half4 albedoAlpha2 = SampleAlbedoAlpha(uv2, TEXTURE2D_ARGS(_BaseMap2, sampler_BaseMap2));
        float alpha2 = albedoAlpha2.a * _TextureScale;
        #ifdef _TEXTURE_BLENDING_ALBEDO
            albedoAlpha = lerp(albedoAlpha, albedoAlpha2, alpha2);
        #endif
    #endif

    #ifdef _PATTERN_BLENDING
        half4 albedoAlpha3 = SampleAlbedoAlpha(uv3, TEXTURE2D_ARGS(_BaseMap3, sampler_BaseMap3));
        float alpha3 = albedoAlpha3.a * _PatternScale;
        #ifdef _PATTERN_BLENDING_ALBEDO
            albedoAlpha = lerp(albedoAlpha, albedoAlpha3, alpha3);
        #endif
    #endif

    outSurfaceData.alpha = Alpha(albedoAlpha.a, _BaseColor, _Cutoff);

    half4 specGloss = SampleMetallicSpecGloss(uv, albedoAlpha.a);
    #ifdef _TEXTURE_BLENDING
        #ifdef _TEXTURE_BLENDING_METALLIC
            half4 specGloss2 = SampleMetallicSpecGloss2(uv2, albedoAlpha.a);
            specGloss = lerp(specGloss, specGloss2, alpha2);
        #endif
    #endif

    #ifdef _PATTERN_BLENDING
        #ifdef _PATTERN_BLENDING_METALLIC
            half4 specGloss3 = SampleMetallicSpecGloss3(uv3, albedoAlpha.a);
            // for Nuan Nuan textures
            specGloss3 = specGloss3.rgbg;
            specGloss = lerp(specGloss, specGloss3, alpha3);
        #endif
    #endif

    outSurfaceData.albedo = albedoAlpha.rgb * _BaseColor.rgb;

    #if _SPECULAR_SETUP
        outSurfaceData.metallic = 1.0h;
        outSurfaceData.specular = specGloss.rgb;
    #else
        outSurfaceData.metallic = specGloss.r;
        outSurfaceData.specular = half3(0.0h, 0.0h, 0.0h);
    #endif

    outSurfaceData.smoothness = specGloss.a;
    //outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);

    half3 bump = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    #ifdef _TEXTURE_BLENDING
        #ifdef _TEXTURE_BLENDING_NORMAL
            half3 bump2 = SampleNormal(uv2, TEXTURE2D_ARGS(_BumpMap2, sampler_BumpMap2), _BumpScale);
            bump = normalize(bump + bump2 * alpha2);
        #endif
    #endif

    #ifdef _PATTERN_BLENDING
        #ifdef _PATTERN_BLENDING_NORMAL
            half3 bump3 = SampleNormal(uv3, TEXTURE2D_ARGS(_BumpMap3, sampler_BumpMap3), _BumpScale);
            bump = normalize(bump + bump3 * alpha3);
        #endif
    #endif

    #ifdef _NORMAL_NOISE
        half4 n = _NoiseNormalScale > 0 ? SAMPLE_TEXTURE2D(_NoiseBumpMap, sampler_NoiseBumpMap, uvNoise) : (0, 0, 0, 0);
        half3 noise_bump = ((n.rg - 0.5) * 2, n.b);
        bump = _NoiseNormalScale > 0 ? normalize(bump + noise_bump * _NoiseNormalScale)  : bump;
    #endif

    outSurfaceData.normalTS = bump;
    outSurfaceData.occlusion = SampleOcclusion(uv);
    outSurfaceData.emission = SampleEmission(uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));
}
#endif

#endif // LIGHTWEIGHT_INPUT_SURFACE_PBR_INCLUDED
