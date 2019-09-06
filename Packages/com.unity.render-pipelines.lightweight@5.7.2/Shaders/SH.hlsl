#ifndef LIGHTWEIGHT_SH_INCLUDED
#define LIGHTWEIGHT_SH_INCLUDED

#include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

#ifdef INTERPOLATE_SH

#ifndef INTERPOLATE_SH_UNIFORMS
#define INTERPOLATE_SH_UNIFORMS
#endif // INTERPOLATE_SH_UNIFORMS

float4 _shPosition;
float4 _sh2Position;
float4 _sh3Position;

float4 SHAr;
float4 SHAg;
float4 SHAb;
float4 SHBr;
float4 SHBg;
float4 SHBb;
float4 SHC;

float4 SH2Ar;
float4 SH2Ag;
float4 SH2Ab;
float4 SH2Br;
float4 SH2Bg;
float4 SH2Bb;
float4 SH2C;

float4 SH3Ar;
float4 SH3Ag;
float4 SH3Ab;
float4 SH3Br;
float4 SH3Bg;
float4 SH3Bb;
float4 SH3C;

// #ifdef INTERPOLATE_SH

inline float distance_square(float3 a, float3 b)
{
    float dx = a.x - b.x;
    float dy = a.y - b.y;
    float dz = a.z - b.z;
    return dx * dx + dy * dy + dz * dz;
}

inline float increase_by_epsilon(float d, float epsilon = 0.000001)
{
    return d > epsilon ? d : d + epsilon;
}

// Interpolate among three sets of SH (Spherical Harmonics)
// Samples SH L0, L1 and L2 terms
half3 Sample3SHs(half3 normalWS, float3 vertex_position)
{
    float3 probe1_position = _shPosition.xyz;
    float3 probe2_position = _sh2Position.xyz;
    float3 probe3_position = _sh3Position.xyz;
    float d1 = distance_square(vertex_position, probe1_position);
    float d2 = distance_square(vertex_position, probe2_position);
    float d3 = distance_square(vertex_position, probe3_position);
    d1 = increase_by_epsilon(d1);
    d2 = increase_by_epsilon(d2);
    d3 = increase_by_epsilon(d3);
    float w1 = 1 / d1;
    float w2 = 1 / d2;
    float w3 = 1 / d3;
    float sum = w1 + w2 + w3;
    float a = w1 / sum;
    float b = w2 / sum;
    float c = w3 / sum;

    real4 SHCoefficients[7];

    // SHCoefficients[0] = unity_SHAr;
    // SHCoefficients[1] = unity_SHAg;
    // SHCoefficients[2] = unity_SHAb;
    // SHCoefficients[3] = unity_SHBr;
    // SHCoefficients[4] = unity_SHBg;
    // SHCoefficients[5] = unity_SHBb;
    // SHCoefficients[6] = unity_SHC;

    // SHCoefficients[0] = SHAr;
    // SHCoefficients[1] = SHAg;
    // SHCoefficients[2] = SHAb;
    // SHCoefficients[3] = SHBr;
    // SHCoefficients[4] = SHBg;
    // SHCoefficients[5] = SHBb;
    // SHCoefficients[6] = SHC;

    SHCoefficients[0] = a * SHAr + b * SH2Ar + c * SH3Ar;
    SHCoefficients[1] = a * SHAg + b * SH2Ag + c * SH3Ag;
    SHCoefficients[2] = a * SHAb + b * SH2Ab + c * SH3Ab;
    SHCoefficients[3] = a * SHBr + b * SH2Br + c * SH3Br;
    SHCoefficients[4] = a * SHBg + b * SH2Bg + c * SH3Bg;
    SHCoefficients[5] = a * SHBb + b * SH2Bb + c * SH3Bb;
    SHCoefficients[6] = a * SHC + b * SH2C + c * SH3C;

    return max(half3(0, 0, 0), SampleSH9(SHCoefficients, normalWS));
}

half3 SampleSHs(half3 normalWS, float3 vertex_position)
{
    return Sample3SHs(normalWS, vertex_position);
}

// Interpolate among three sets of SH (Spherical Harmonics)
// Samples SH L0, L1 and L2 terms
void SampleSHs_Array(half3 normalWS, float3 vertex_position, inout real4 SHCoefficients[7])
{
    float3 probe1_position = _shPosition.xyz;
    float3 probe2_position = _sh2Position.xyz;
    float3 probe3_position = _sh3Position.xyz;
    float d1 = distance_square(vertex_position, probe1_position);
    float d2 = distance_square(vertex_position, probe2_position);
    float d3 = distance_square(vertex_position, probe3_position);
    d1 = increase_by_epsilon(d1);
    d2 = increase_by_epsilon(d2);
    d3 = increase_by_epsilon(d3);
    float w1 = 1 / d1;
    float w2 = 1 / d2;
    float w3 = 1 / d3;
    float sum = w1 + w2 + w3;
    float a = w1 / sum;
    float b = w2 / sum;
    float c = w3 / sum;

    // real4 SHCoefficients[7];

    SHCoefficients[0] = a * SHAr + b * SH2Ar + c * SH3Ar;
    SHCoefficients[1] = a * SHAg + b * SH2Ag + c * SH3Ag;
    SHCoefficients[2] = a * SHAb + b * SH2Ab + c * SH3Ab;
    SHCoefficients[3] = a * SHBr + b * SH2Br + c * SH3Br;
    SHCoefficients[4] = a * SHBg + b * SH2Bg + c * SH3Bg;
    SHCoefficients[5] = a * SHBb + b * SH2Bb + c * SH3Bb;
    SHCoefficients[6] = a * SHC + b * SH2C + c * SH3C;

    // return SHCoefficients;

    // return max(half3(0, 0, 0), SampleSH9(SHCoefficients, normalWS));
}

#endif // INTERPOLATE_SH

#endif // LIGHTWEIGHT_SH_INCLUDED
