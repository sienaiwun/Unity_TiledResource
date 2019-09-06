#ifndef LIGHTWEIGHT_ONMYOJI_LIGHTING_LIB_INCLUDED
#define LIGHTWEIGHT_ONMYOJI_LIGHTING_LIB_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
#include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/Core.hlsl"

#ifdef PLANER_REFLECTION
    TEXTURE2D(_PlanarReflectionTexture);            SAMPLER(sampler_PlanarReflectionTexture);
    #ifdef _GLOSSY_REFLECTION
        TEXTURE2D(_PlanarReflectionBlurTexture);      SAMPLER(sampler_PlanarReflectionBlurTexture);
        TEXTURE2D(_PlanarReflectionDepth);            SAMPLER(sampler_PlanarReflectionDepth);
        float4x4 _Reflect_ViewProjectInverse;
        float4 _Reflect_Plane;
        float _Fade_Dis,_Cubemap_Fade_Dis_Radio;
    #endif
#endif

// If lightmap is not defined than we evaluate GI (ambient + probes) from SH
// We might do it fully or partially in vertex to save shader ALU
#if !defined(LIGHTMAP_ON)
// TODO: Controls things like these by exposing SHADER_QUALITY levels (low, medium, high)
    #if defined(SHADER_API_GLES) || !defined(_NORMALMAP)
        // Evaluates SH fully in vertex
        #define EVALUATE_SH_VERTEX
    #elif !SHADER_HINT_NICE_QUALITY
        // Evaluates L2 SH in vertex and L0L1 in pixel
        #define EVALUATE_SH_MIXED
    #endif
        // Otherwise evaluate SH fully per-pixel
#endif

#define ONMYOJI_PI 3.1415926
#define ONMYOJI_INV_PI 0.31830988618

// Enable indirect diffuse from cubemap
#ifndef CUBEMAP_INDIRECT_DIFFUSE
#define CUBEMAP_INDIRECT_DIFFUSE
    int CUBEMAP_EXTRA = 0;
    half4 CubemapExtraColor = half4(1, 1, 1, 1);
#endif // CUBEMAP_INDIRECT_DIFFUSE

///////////////////////////////////////////////////////////////////////////////
//                          Light Helpers                                    //
///////////////////////////////////////////////////////////////////////////////

// Abstraction over Light shading data.
struct Light
{
    half3 direction;
    half3 color;
    half distanceAttenuation;
    half shadowAttenuation;
};

int GetPerObjectLightIndex(int index)
{
    // The following code is more optimal than indexing unity_4LightIndices0.
    // Conditional moves are branch free even on mali-400
    half2 lightIndex2 = (index < 2.0h) ? unity_LightIndices[0].xy : unity_LightIndices[0].zw;
    half i_rem = (index < 2.0h) ? index : index - 2.0h;
    return (i_rem < 1.0h) ? lightIndex2.x : lightIndex2.y;
}

///////////////////////////////////////////////////////////////////////////////
//                        Attenuation Functions                               /
///////////////////////////////////////////////////////////////////////////////

// Matches Unity Vanila attenuation
// Attenuation smoothly decreases to light range.
half DistanceAttenuation(half distanceSqr, half2 distanceAttenuation)
{
    // We use a shared distance attenuation for additional directional and puctual lights
    // for directional lights attenuation will be 1
    half lightAtten = rcp(distanceSqr);

#if defined(SHADER_HINT_NICE_QUALITY)
    // Use the smoothing factor also used in the Unity lightmapper.
    half factor = distanceSqr * distanceAttenuation.x;
    half smoothFactor = saturate(1.0h - factor * factor);
    smoothFactor = smoothFactor * smoothFactor;
#else
    // We need to smoothly fade attenuation to light range. We start fading linearly at 80% of light range
    // Therefore:
    // fadeDistance = (0.8 * 0.8 * lightRangeSq)
    // smoothFactor = (lightRangeSqr - distanceSqr) / (lightRangeSqr - fadeDistance)
    // We can rewrite that to fit a MAD by doing
    // distanceSqr * (1.0 / (fadeDistanceSqr - lightRangeSqr)) + (-lightRangeSqr / (fadeDistanceSqr - lightRangeSqr)
    // distanceSqr *        distanceAttenuation.y            +             distanceAttenuation.z
    half smoothFactor = saturate(distanceSqr * distanceAttenuation.x + distanceAttenuation.y);
#endif

    return lightAtten * smoothFactor;
}

half AngleAttenuation(half3 spotDirection, half3 lightDirection, half2 spotAttenuation)
{
    // Spot Attenuation with a linear falloff can be defined as
    // (SdotL - cosOuterAngle) / (cosInnerAngle - cosOuterAngle)
    // This can be rewritten as
    // invAngleRange = 1.0 / (cosInnerAngle - cosOuterAngle)
    // SdotL * invAngleRange + (-cosOuterAngle * invAngleRange)
    // SdotL * spotAttenuation.x + spotAttenuation.y

    // If we precompute the terms in a MAD instruction
    half SdotL = dot(spotDirection, lightDirection);
    half atten = saturate(SdotL * spotAttenuation.x + spotAttenuation.y);
    return atten * atten;
}

///////////////////////////////////////////////////////////////////////////////
//                      Light Abstraction                                    //
///////////////////////////////////////////////////////////////////////////////

Light GetMainLight()
{
    Light light;
    light.direction = _MainLightPosition.xyz;
    light.distanceAttenuation = unity_LightData.z;
    light.shadowAttenuation = 1.0;
    light.color = _MainLightColor.rgb;

    return light;
}

Light GetMainLight(float4 shadowCoord)
{
    Light light = GetMainLight();
    light.shadowAttenuation = MainLightRealtimeShadow(shadowCoord);
    return light;
}

Light GetAdditionalLight(int i, float3 positionWS)
{
    int perObjectLightIndex = GetPerObjectLightIndex(i);

    // The following code will turn into a branching madhouse on platforms that don't support
    // dynamic indexing. Ideally we need to configure light data at a cluster of
    // objects granularity level. We will only be able to do that when scriptable culling kicks in.
    // TODO: Use StructuredBuffer on PC/Console and profile access speed on mobile that support it.
    // Abstraction over Light input constants
    float3 lightPositionWS = _AdditionalLightsPosition[perObjectLightIndex].xyz;
    half4 distanceAndSpotAttenuation = _AdditionalLightsAttenuation[perObjectLightIndex];
    half4 spotDirection = _AdditionalLightsSpotDir[perObjectLightIndex];

    float3 lightVector = lightPositionWS - positionWS;
    float distanceSqr = max(dot(lightVector, lightVector), HALF_MIN);

    half3 lightDirection = half3(lightVector * rsqrt(distanceSqr));
    half attenuation = DistanceAttenuation(distanceSqr, distanceAndSpotAttenuation.xy);
    attenuation *= AngleAttenuation(spotDirection.xyz, lightDirection, distanceAndSpotAttenuation.zw);

    Light light;
    light.direction = lightDirection;
    light.distanceAttenuation = attenuation;
    light.shadowAttenuation = AdditionalLightRealtimeShadow(perObjectLightIndex, positionWS);
    light.color = _AdditionalLightsColor[perObjectLightIndex].rgb;

    return light;
}

int GetAdditionalLightsCount()
{
    // TODO: we need to expose in SRP api an ability for the pipeline cap the amount of lights
    // in the culling. This way we could do the loop branch with an uniform
    // This would be helpful to support baking exceeding lights in SH as well
    return min(_AdditionalLightsCount.x, unity_LightData.y);
}

half3 OnmyojiEnvBRDFApprox(half3 SpecularColor, half Roughness, half NoV)
{
	// [ Lazarov 2013, "Getting More Physical in Call of Duty: Black Ops II" ]
	// Adaptation to fit our G term.
    const half4 c0 = { -1, -0.0275, -0.572, 0.022 };
    const half4 c1 = { 1, 0.0425, 1.04, -0.04 };
    half4 r = Roughness * c0 + c1;
    half a004 = min(r.x * r.x, exp2(-9.28 * NoV)) * r.x + r.y;
    half2 AB = half2(-1.04, 1.04) * a004 + r.zw;

	// Anything less than 2% is physically impossible and is instead considered to be shadowing
	// Note: this is needed for the 'specular' show flag to work, since it uses a SpecularColor of 0
    AB.y *= saturate(50.0 * SpecularColor.g);

    return SpecularColor * AB.x + AB.y;
}

half3 OnmyojiEnvironmentBRDF(half3 diffuseColor, half3 specularColor, half perceptualRoughness, half3 indirectDiffuse, half3 indirectSpecular, half NoV)
{
    half3 c = indirectDiffuse * diffuseColor;
    c += indirectSpecular * OnmyojiEnvBRDFApprox(specularColor, perceptualRoughness, NoV);
    return c;
}

float Vis_SmithJointApprox(float Roughness, float NoV, float NoL)
{
    float a = Roughness * Roughness;
    float Vis_SmithV = NoL * (NoV * (1 - a) + a);
    float Vis_SmithL = NoV * (NoL * (1 - a) + a);
	// Note: will generate NaNs with Roughness = 0.  MinRoughness is used to prevent this
    return 0.5 * rcp(Vis_SmithV + Vis_SmithL + 0.001);
}

// [Schlick 1994, "An Inexpensive BRDF Model for Physically-Based Rendering"]
float3 F_Schlick_Onmyoji(float3 SpecularColor, float VoH)
{
    float Fc = (1 - VoH);
    Fc = Fc * Fc * Fc * Fc * Fc; // 1 sub, 3 mul
	//return Fc + (1 - Fc) * SpecularColor;		// 1 add, 3 mad
	
	// Anything less than 2% is physically impossible and is instead considered to be shadowing
    return saturate(50.0 * SpecularColor.g) * Fc + (1 - Fc) * SpecularColor;
	
}

// GGX / Trowbridge-Reitz
// [Walter et al. 2007, "Microfacet models for refraction through rough surfaces"]
float D_GGX_Onmyoji(float Roughness, float NoH)
{
    float a = Roughness * Roughness;
    float a2 = a * a;
    float d = (NoH * a2 - NoH) * NoH + 1; // 2 mad
    return a2 / (d * d) * ONMYOJI_INV_PI; // 4 mul, 1 rcp
}

float3 Diffuse_Lambert(float3 DiffuseColor)
{
    return DiffuseColor * ONMYOJI_INV_PI;
}

float3 OnmyojiStandardShading(float3 DiffuseColor, float3 SpecularColor, float PerceptualRoughness, float3 L, float3 V, half3 N)
{
    // Unreal uses this max to prevent smooth object completely dark
    PerceptualRoughness = max(PerceptualRoughness, 0.04);
    float NoL = dot(N, L);
    float NoV = dot(N, V);
    float LoV = dot(L, V);
    float InvLenH = rsqrt(2 + 2 * LoV);
    float NoH = saturate((NoL + NoV) * InvLenH);
    float VoH = saturate(InvLenH + InvLenH * LoV);
    NoL = saturate(abs(NoL) + 1e-5);
    NoV = saturate(abs(NoV) + 1e-5);

	// Generalized microfacet specular
    float D = D_GGX_Onmyoji(PerceptualRoughness, NoH);
    float Vis = Vis_SmithJointApprox(PerceptualRoughness, NoV, NoL);
    float3 F = F_Schlick_Onmyoji(SpecularColor, VoH);

    float3 Diffuse = Diffuse_Lambert(DiffuseColor);
	//float3 Diffuse = Diffuse_Burley( DiffuseColor, LobeRoughness[1], NoV, NoL, VoH );
	//float3 Diffuse = Diffuse_OrenNayar( DiffuseColor, LobeRoughness[1], NoV, NoL, VoH );

    return (Diffuse + (D * Vis) * F) * ONMYOJI_PI;
}

half3 OnmyojiDirectBDRF(half3 diffuseColor, half3 specularColor, half perceptualRoughness, half3 lightDirectionWS, half3 viewDirectionWS, half3 normalWS)
{
    half3 color = OnmyojiStandardShading(diffuseColor, specularColor, perceptualRoughness, lightDirectionWS, viewDirectionWS, normalWS);
    return color;
}

#define REFLECTION_CAPTURE_ROUGHEST_MIP 1
#define REFLECTION_CAPTURE_ROUGHNESS_MIP_SCALE 1.2
#define ONMYOJI_SPECCUBE_LOD_STEPS 9
half OnmyojiRoughnessToMipmapLevel(half Roughness)
{
    half LevelFrom1x1 = REFLECTION_CAPTURE_ROUGHEST_MIP - REFLECTION_CAPTURE_ROUGHNESS_MIP_SCALE * log2(Roughness);
    return ONMYOJI_SPECCUBE_LOD_STEPS - 1 - LevelFrom1x1;
}

float3 OnmyojiGetOffSpecularPeakReflectionDir(float3 Normal, float3 ReflectionVector, float Roughness)
{
    float a = saturate(Roughness * Roughness);
    return lerp(Normal, ReflectionVector, (1 - a) * (sqrt(1 - a) + a));
}

half3 OnmyojiGlossyEnvironmentReflection(half3 reflectVector, half roughness, half occlusion)
{
#if !defined(_ENVIRONMENTREFLECTIONS_OFF)
	half mip = OnmyojiRoughnessToMipmapLevel(roughness);
	half4 encodedIrradiance = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectVector, mip);

#if !defined(UNITY_USE_NATIVE_HDR)
	half3 irradiance = DecodeHDREnvironment(encodedIrradiance, unity_SpecCube0_HDR);
#else
	half3 irradiance = encodedIrradiance;
#endif

	return irradiance * occlusion;
#endif // GLOSSY_REFLECTIONS

	return _GlossyEnvironmentColor.rgb * occlusion;
}

#ifdef CUBEMAP_INDIRECT_DIFFUSE

half3 CubemapIndirectDiffuse(half3 indirectDiffuse, half3 normalWS, half occlusion)
{
    indirectDiffuse *= CUBEMAP_EXTRA == 1 ? (1 + OnmyojiGlossyEnvironmentReflection(normalWS, 0.7, occlusion)) * CubemapExtraColor.rgb : 1;
    return indirectDiffuse;
}

half3 CubemapIndirectSpecular(half3 indirectSpecular)
{
    indirectSpecular *= CUBEMAP_EXTRA == 1 ? CubemapExtraColor.rgb : 1;
    return indirectSpecular;
}

#endif // CUBEMAP_INDIRECT_DIFFUSE

// Global illumination diffuse
// Samples SH L0, L1 and L2 terms
half3 SampleSH(half3 normalWS)
{
    // LPPV is not supported in Ligthweight Pipeline
    real4 SHCoefficients[7];
    SHCoefficients[0] = unity_SHAr;
    SHCoefficients[1] = unity_SHAg;
    SHCoefficients[2] = unity_SHAb;
    SHCoefficients[3] = unity_SHBr;
    SHCoefficients[4] = unity_SHBg;
    SHCoefficients[5] = unity_SHBb;
    SHCoefficients[6] = unity_SHC;

    return max(half3(0, 0, 0), SampleSH9(SHCoefficients, normalWS));
}

// SH Vertex Evaluation. Depending on target SH sampling might be
// done completely per vertex or mixed with L2 term per vertex and L0, L1
// per pixel. See SampleSHPixel
half3 SampleSHVertex(half3 normalWS)
{
#if defined(EVALUATE_SH_VERTEX)
    return max(half3(0, 0, 0), SampleSH(normalWS));
#elif defined(EVALUATE_SH_MIXED)
    // no max since this is only L2 contribution
    return SHEvalLinearL2(normalWS, unity_SHBr, unity_SHBg, unity_SHBb, unity_SHC);
#endif

    // Fully per-pixel. Nothing to compute.
    return half3(0.0, 0.0, 0.0);
}

// SH Pixel Evaluation. Depending on target SH sampling might be done
// mixed or fully in pixel. See SampleSHVertex
half3 SampleSHPixel(half3 L2Term, half3 normalWS)
{
#if defined(EVALUATE_SH_VERTEX)
    return L2Term;
#elif defined(EVALUATE_SH_MIXED)
    half3 L0L1Term = SHEvalLinearL0L1(normalWS, unity_SHAr, unity_SHAg, unity_SHAb);
    return max(half3(0, 0, 0), L2Term + L0L1Term);
#endif

    // Default: Evaluate SH fully per-pixel
    return SampleSH(normalWS);
}


#if defined(LIGHTMAP_ON) && defined(SHADOWS_SHADOWMASK)
half4 SampleShadowMask(float2 lightmapUV)
{
	return SAMPLE_TEXTURE2D(unity_ShadowMask, samplerunity_ShadowMask, lightmapUV);
}
#endif
// Sample baked lightmap. Non-Direction and Directional if available.
// Realtime GI is not supported.
half3 SampleLightmap(float2 lightmapUV, half3 normalWS)
{
#ifdef UNITY_LIGHTMAP_FULL_HDR
    bool encodedLightmap = false;
#else
    bool encodedLightmap = true;
#endif

    half4 decodeInstructions = half4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0h, 0.0h);

    // The shader library sample lightmap functions transform the lightmap uv coords to apply bias and scale.
    // However, lightweight pipeline already transformed those coords in vertex. We pass half4(1, 1, 0, 0) and
    // the compiler will optimize the transform away.
    half4 transformCoords = half4(1, 1, 0, 0);
    half3 lightmapColor = half3(0.0, 0.0, 0.0);
 
#ifdef DIRLIGHTMAP_COMBINED
    lightmapColor = SampleDirectionalLightmap(TEXTURE2D_ARGS(unity_Lightmap, samplerunity_Lightmap),
        TEXTURE2D_ARGS(unity_LightmapInd, samplerunity_Lightmap),
        lightmapUV, transformCoords, normalWS, encodedLightmap, decodeInstructions);
#elif defined(LIGHTMAP_ON)
    lightmapColor = SampleSingleLightmap(TEXTURE2D_ARGS(unity_Lightmap, samplerunity_Lightmap), lightmapUV, transformCoords, encodedLightmap, decodeInstructions);   
#endif // DIRLIGHTMAP_COMBINED

#ifdef _LIGHTMAP_DAY_CYCLE
    half3 lightmap2Color;
#ifdef DIRLIGHTMAP_COMBINED
    lightmap2Color = SampleDirectionalLightmap(TEXTURE2D_ARGS(_dayCycle_Lightmap, samplerunity_Lightmap),
        TEXTURE2D_ARGS(unity_LightmapInd, samplerunity_Lightmap),
        lightmapUV, transformCoords, normalWS, encodedLightmap, decodeInstructions);
#elif defined(LIGHTMAP_ON)
    lightmap2Color = SampleSingleLightmap(TEXTURE2D_ARGS(_dayCycle_Lightmap, samplerunity_Lightmap), lightmapUV, transformCoords, encodedLightmap, decodeInstructions);
#endif // DIRLIGHTMAP_COMBINED
    lightmapColor = lightmapColor * (1-_DayCycleFactor) + lightmap2Color * _DayCycleFactor; // dayCycleFactor
#endif //_LIGHTMAP_DAY_CYCLE
    return lightmapColor;
}

// We either sample GI from baked lightmap or from probes.
// If lightmap: sampleData.xy = lightmapUV
// If probe: sampleData.xyz = L2 SH terms
#ifdef LIGHTMAP_ON
    #define SAMPLE_GI(lmName, shName, normalWSName) SampleLightmap(lmName, normalWSName)
#else
    #define SAMPLE_GI(lmName, shName, normalWSName) SampleSHPixel(shName, normalWSName)
#endif

#ifdef LIGHTMAP_ON
    #define DECLARE_LIGHTMAP_OR_SH(lmName, shName, index) float2 lmName : TEXCOORD##index
    #define OUTPUT_LIGHTMAP_UV(lightmapUV, lightmapScaleOffset, OUT) OUT.xy = lightmapUV.xy * lightmapScaleOffset.xy + lightmapScaleOffset.zw;
    #define OUTPUT_SH(normalWS, OUT)
#else
    #define DECLARE_LIGHTMAP_OR_SH(lmName, shName, index) half3 shName : TEXCOORD##index
    #define OUTPUT_LIGHTMAP_UV(lightmapUV, lightmapScaleOffset, OUT)
    #define OUTPUT_SH(normalWS, OUT) OUT.xyz = SampleSHVertex(normalWS)
#endif

half3 GlossyEnvironmentReflection(half3 reflectVector, half perceptualRoughness, half occlusion)
{
#if !defined(_ENVIRONMENTREFLECTIONS_OFF)
    half mip = PerceptualRoughnessToMipmapLevel(perceptualRoughness);
    half4 encodedIrradiance = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectVector, mip);

#if !defined(UNITY_USE_NATIVE_HDR)
    half3 irradiance = DecodeHDREnvironment(encodedIrradiance, unity_SpecCube0_HDR);
#else
    half3 irradiance = encodedIrradiance.rbg;
#endif

    return irradiance * occlusion;
#endif // GLOSSY_REFLECTIONS

    return _GlossyEnvironmentColor.rgb * occlusion;
}

#if defined(PLANER_REFLECTION)
half3 GlossyEnvironmentReflection(half3 reflectVector, half2 screenPos, half perceptualRoughness, half occlusion)
{
#if defined(_GLOSSY_REFLECTION)
        float3 cubemapReflection = OnmyojiGlossyEnvironmentReflection(reflectVector,perceptualRoughness,occlusion);
        float depth = SAMPLE_TEXTURE2D(_PlanarReflectionDepth, sampler_PlanarReflectionDepth,screenPos).r;
        float4 H = float4((screenPos.x) * 2 - 1, (screenPos.y) * 2 - 1, depth, 1.0);
        float4 D = mul(_Reflect_ViewProjectInverse, H);
        float3 refpos = D.xyz / D.w;
        float distance = dot(float4(refpos.xyz,1.0f),_Reflect_Plane);
        if(distance<0.0)
            return cubemapReflection;
        float fade_by_depth = saturate(distance/_Fade_Dis);
        half mip = PerceptualRoughnessToMipmapLevel(perceptualRoughness);
        half4 irradiance_clear = SAMPLE_TEXTURE2D_LOD(_PlanarReflectionTexture, sampler_PlanarReflectionTexture, screenPos, mip);
        half4 irradiance_blur = SAMPLE_TEXTURE2D_LOD(_PlanarReflectionBlurTexture, sampler_PlanarReflectionBlurTexture, screenPos, mip + fade_by_depth * 2.0);
        half4 irradiance = lerp(irradiance_clear,irradiance_blur,fade_by_depth);
        half4 planerReflection = irradiance * occlusion;
        fade_by_depth /= _Cubemap_Fade_Dis_Radio;
        return lerp(planerReflection,cubemapReflection,fade_by_depth);
#else
        half mip = PerceptualRoughnessToMipmapLevel(perceptualRoughness);
        half4 irradiance_clear = SAMPLE_TEXTURE2D_LOD(_PlanarReflectionTexture, sampler_PlanarReflectionTexture, screenPos, mip);
        return irradiance_clear.rgb * occlusion;
#endif
}
#endif

half3 SubtractDirectMainLightFromLightmap(Light mainLight, half3 normalWS, half3 bakedGI)
{
    // Let's try to make realtime shadows work on a surface, which already contains
    // baked lighting and shadowing from the main sun light.
    // Summary:
    // 1) Calculate possible value in the shadow by subtracting estimated light contribution from the places occluded by realtime shadow:
    //      a) preserves other baked lights and light bounces
    //      b) eliminates shadows on the geometry facing away from the light
    // 2) Clamp against user defined ShadowColor.
    // 3) Pick original lightmap value, if it is the darkest one.


    // 1) Gives good estimate of illumination as if light would've been shadowed during the bake.
    // We only subtract the main direction light. This is accounted in the contribution term below.
    half shadowStrength = GetMainLightShadowStrength();
    half contributionTerm = saturate(dot(mainLight.direction, normalWS));
    half3 lambert = mainLight.color * contributionTerm;
    half3 estimatedLightContributionMaskedByInverseOfShadow = lambert * (1.0 - mainLight.shadowAttenuation);
    half3 subtractedLightmap = bakedGI - estimatedLightContributionMaskedByInverseOfShadow;

    // 2) Allows user to define overall ambient of the scene and control situation when realtime shadow becomes too dark.
    half3 realtimeShadow = max(subtractedLightmap, _SubtractiveShadowColor.xyz);
    realtimeShadow = lerp(bakedGI, realtimeShadow, shadowStrength);

    // 3) Pick darkest color
    return min(bakedGI, realtimeShadow);
}

#endif // LIGHTWEIGHT_ONMYOJI_LIGHTING_LIB_INCLUDED
