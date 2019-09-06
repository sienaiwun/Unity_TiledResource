Shader "Hidden/Lightweight Render Pipeline/SSDownSample"
{
    Properties
    {
        _MainTex("Albedo", 2D) = "white" {}
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/Core.hlsl"


    struct Attributes
    {
        float4 positionOS   : POSITION;
        float2 texcoord     : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4  positionCS  : SV_POSITION;
        float2  uv          : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    Varyings Vertex(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_TRANSFER_INSTANCE_ID(input, output);

        output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
        output.uv = input.texcoord;
        return output;
    }

    half4 DownsampleBox5Tap(TEXTURE2D_PARAM(tex, samplerTex), float2 uv, float2 texelSize, float amount)
    {
        float4 d = texelSize.xyxy * float4(-amount, -amount, amount, amount);

        half4 s;
        s =  (SAMPLE_TEXTURE2D(tex, samplerTex, uv + d.xy));
        s += (SAMPLE_TEXTURE2D(tex, samplerTex, uv + d.zy));
        s += (SAMPLE_TEXTURE2D(tex, samplerTex, uv + d.xw));
        s += (SAMPLE_TEXTURE2D(tex, samplerTex, uv + d.zw));
		s += (SAMPLE_TEXTURE2D(tex, samplerTex, uv));

        return s * 0.2h;
    }

	half4 blur13(TEXTURE2D_PARAM(tex, samplerTex), float2 uv, float2 resolution) {
		float2 direction = float2(1.0, 1.0);
		half4 color;
		float2 off1 = float2(direction * 1.411764705882353);
		float2 off2 = float2(direction * 3.2941176470588234) ;
		float2 off3 = float2(direction * 5.176470588235294) ;
		//color = texture2D(image, uv) * 0.1964825501511404;
		color = (SAMPLE_TEXTURE2D(tex, samplerTex, uv)) * 0.1964825501511404;
		//color += texture2D(image, uv + (off1 / resolution)) * 0.2969069646728344;
		color += (SAMPLE_TEXTURE2D(tex, samplerTex, uv + (off1 / resolution))) * 0.2969069646728344;
		//color += texture2D(image, uv - (off1 / resolution)) * 0.2969069646728344;
		color += (SAMPLE_TEXTURE2D(tex, samplerTex, uv - (off1 / resolution))) * 0.2969069646728344;
		//color += texture2D(image, uv + (off2 / resolution)) * 0.09447039785044732;
		color += (SAMPLE_TEXTURE2D(tex, samplerTex, uv + (off2 / resolution))) * 0.09447039785044732;
		//color += texture2D(image, uv - (off2 / resolution)) * 0.09447039785044732;
		color += (SAMPLE_TEXTURE2D(tex, samplerTex, uv - (off2 / resolution))) * 0.09447039785044732;
		//color += texture2D(image, uv + (off3 / resolution)) * 0.010381362401148057;
		color += (SAMPLE_TEXTURE2D(tex, samplerTex, uv + (off3 / resolution))) *0.010381362401148057;
		//color += texture2D(image, uv - (off3 / resolution)) * 0.010381362401148057;
		color += (SAMPLE_TEXTURE2D(tex, samplerTex, uv - (off3 / resolution))) *0.010381362401148057;
		return color;
	}

	float normpdf(in float x, in float sigma)
	{
		return 0.39894*exp(-0.5*x*x / (sigma*sigma)) / sigma;
	}

	half4 GassianBlur(TEXTURE2D_PARAM(tex, samplerTex), float2 uv, float2 resolution) {
		half4 col = half4(0.0, 0.0, 0.0, 0.0);
		const int mSize = 65;
		const int kSize = (mSize - 1) / 2;
		float kernel[mSize];
		float3 final_colour = float3(0.0, 0.0, 0.0);

		//create the 1-D kernel
		float sigma = 5.0;
		float Z = 0.0;
		for (int j = 0; j <= kSize; ++j)
		{
			kernel[kSize + j] = kernel[kSize - j] = normpdf(float(j), sigma);
		}

		//get the normalization factor (as the gaussian has been clamped)
		for (int j = 0; j < mSize; ++j)
		{
			Z += kernel[j];
		}
		//read out the texels
		for (int i = -kSize; i <= kSize; ++i)
		{
			for (int j = -kSize; j <= kSize; ++j)
			{
				float2 offset = float2(float(i), float(j)) / resolution;
				half4 sample_color = SAMPLE_TEXTURE2D(tex, samplerTex, (uv + offset));
				col += kernel[kSize + j] * kernel[kSize + i] * sample_color;
			}
		}
		col.rgb = col.rgb / (Z*Z);
		col.a = 1;
		return col;
	}


    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "LightweightPipeline"}
        LOD 100

        // 0 - Downsample - Box filtering
        Pass
        {
            Name "SSDownSample"
            ZTest Always
            ZWrite Off

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma vertex Vertex
            #pragma fragment FragBoxDownsample

            TEXTURE2D(_ScreenSpaceShadowmapTexture);
            SAMPLER(sampler_ScreenSpaceShadowmapTexture);
            float4 _ScreenSpaceShadowmapTexture_TexelSize;

            float _SampleOffset;

            half4 FragBoxDownsample(Varyings input) : SV_Target
            {
               // half4 col = DownsampleBox5Tap(TEXTURE2D_ARGS(_ScreenSpaceShadowmapTexture, sampler_ScreenSpaceShadowmapTexture), input.uv, _ScreenSpaceShadowmapTexture_TexelSize.xy, 1);
				//half4 col = blur13(TEXTURE2D_ARGS(_ScreenSpaceShadowmapTexture, sampler_ScreenSpaceShadowmapTexture), input.uv, _ScreenSpaceShadowmapTexture_TexelSize.zw);
				half4 col = GassianBlur(TEXTURE2D_ARGS(_ScreenSpaceShadowmapTexture, sampler_ScreenSpaceShadowmapTexture), input.uv, _ScreenSpaceShadowmapTexture_TexelSize.zw);
				return col;
				
            }
            ENDHLSL
        }
		

    }
}
