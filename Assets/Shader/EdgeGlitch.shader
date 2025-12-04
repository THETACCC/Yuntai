Shader "ShaderLab/EdgeGlitch"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color   ("Tint", Color) = (1,1,1,1)

        _EdgeThickness ("Edge Thickness (texels)", Range(0.3, 4.0)) = 1.0

        _WaveAmplitude ("Wave Amplitude", Range(0, 0.02)) = 0.003
        _WaveFrequency ("Wave Frequency", Range(1, 40))   = 18
        _WaveSpeed     ("Wave Speed",     Range(0, 10))   = 2.5

        _EdgeThreshold ("Edge Threshold", Range(0.01, 0.3)) = 0.06

        _RandomSeed ("Random Seed", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                float4 _Color;

                float  _EdgeThickness;
                float  _WaveAmplitude;
                float  _WaveFrequency;
                float  _WaveSpeed;
                float  _EdgeThreshold;
                float  _RandomSeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv   = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            float4 frag (Varyings i) : SV_Target
            {
                float2 uv = i.uv;

                float4 baseCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                if (baseCol.a <= 0.001)
                    return float4(0,0,0,0);

                float2 texel = _MainTex_TexelSize.xy * _EdgeThickness;

                float aC = baseCol.a;
                float aL = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-texel.x, 0)).a;
                float aR = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( texel.x, 0)).a;
                float aB = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0, -texel.y)).a;
                float aT = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0,  texel.y)).a;

                float2 grad;
                grad.x = aR - aL;
                grad.y = aT - aB;

                float edgeStrength = length(grad);

                float edgeMask = step(_EdgeThreshold, edgeStrength);
                if (edgeMask <= 0.0)
                    return float4(0,0,0,0);

                float2 normal = (edgeStrength > 0.0001) ? normalize(grad) : float2(0,0);

                float along = dot(uv, float2(1,1));

                float wave = sin(along * _WaveFrequency + _Time.y * _WaveSpeed + _RandomSeed);
                float2 uvOffset = normal * (wave * _WaveAmplitude);

                float2 warpedUV = clamp(uv + uvOffset, 0.0, 1.0);

                float4 warpedCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, warpedUV);

                float4 outCol;
                outCol.rgb = warpedCol.rgb * i.color.rgb;
                outCol.a   = warpedCol.a * edgeMask;

                return outCol;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
