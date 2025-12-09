Shader "ShaderLab/BlackAndWhite"
{
    Properties
    {
        // grayscale shaping
        _Exposure   ("Exposure",   Range(0.5, 2.5)) = 1.2
        _Gamma      ("Gamma",      Range(0.5, 2.0)) = 1.0

        // edge detection
        _EdgeStrength   ("Edge Strength",   Range(0, 10))   = 4.0
        _EdgeThreshold  ("Edge Threshold",  Range(0.01,0.3)) = 0.06
        _LineDarkening  ("Line Darkening",  Range(0, 1.0))   = 0.7

        // final tweak
        _Contrast   ("Contrast",   Range(0.5, 2.0)) = 1.0
        _Brightness ("Brightness", Range(-0.4,0.4)) = 0.0

        // keep red highlights
        _PreserveRedStrength ("Preserve Red Strength", Range(0,1)) = 1.0
        _RedDominanceMin     ("Red Dominance Min",     Range(0,1)) = 0.15
        _RedSaturationMin    ("Red Saturation Min",    Range(0,1)) = 0.5
        _RedValueMin         ("Red Value Min",         Range(0,1)) = 0.3

        // NEW: 整体效果强度
        _EffectIntensity ("Effect Intensity", Range(0,1)) = 1.0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }

        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BlitTexture_TexelSize; // (1/width, 1/height, width, height)

                float  _Exposure;
                float  _Gamma;

                float  _EdgeStrength;
                float  _EdgeThreshold;
                float  _LineDarkening;

                float  _Contrast;
                float  _Brightness;

                float  _PreserveRedStrength;
                float  _RedDominanceMin;
                float  _RedSaturationMin;
                float  _RedValueMin;

                float  _EffectIntensity;   // 0..1
            CBUFFER_END

            TEXTURE2D(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            struct MeshData
            {
                uint vertexID : SV_VertexID;
            };

            struct Interpolators
            {
                float4 posCS : SV_POSITION;
                float2 uv    : TEXCOORD0;
            };

            Interpolators vert (MeshData v)
            {
                Interpolators o;
                o.posCS = GetFullScreenTriangleVertexPosition(v.vertexID);
                o.uv    = GetFullScreenTriangleTexCoord   (v.vertexID);
                return o;
            }

            float get_luminance (float3 color)
            {
                float3 w = float3(0.2126, 0.7152, 0.0722);
                return dot(color, w);
            }

            float4 frag (Interpolators i) : SV_Target
            {
                float2 uv = i.uv;
                float3 rgb = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv).rgb;

                // base grayscale pipeline
                float  L   = get_luminance(rgb);

                // exposure + gamma
                L = saturate(L * _Exposure);
                L = pow(L, _Gamma);

                // Sobel on luminance to get edges
                float2 texel = _BlitTexture_TexelSize.xy;

                float L00 = get_luminance(SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + texel * float2(-1,-1)).rgb);
                float L10 = get_luminance(SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + texel * float2( 0,-1)).rgb);
                float L20 = get_luminance(SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + texel * float2( 1,-1)).rgb);

                float L01 = get_luminance(SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + texel * float2(-1, 0)).rgb);
                float L21 = get_luminance(SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + texel * float2( 1, 0)).rgb);

                float L02 = get_luminance(SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + texel * float2(-1, 1)).rgb);
                float L12 = get_luminance(SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + texel * float2( 0, 1)).rgb);
                float L22 = get_luminance(SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + texel * float2( 1, 1)).rgb);

                float Gx = -L00 + L20 - 2.0 * L01 + 2.0 * L21 - L02 + L22;
                float Gy = -L00 - 2.0 * L10 - L20 + L02 + 2.0 * L12 + L22;

                float edgeMag = length(float2(Gx, Gy));

                // map edgeMag: 0..1
                float edge = saturate((edgeMag - _EdgeThreshold) * _EdgeStrength);

                // darken along line edges
                float lineFactor = 1.0 - edge * _LineDarkening;
                float gray = L * lineFactor;

                // final contrast / brightness
                gray = (gray - 0.5) * _Contrast + 0.5 + _Brightness;
                gray = saturate(gray);

                float3 grayRGB = gray.xxx;

                // preserve red highlights
                float maxC = max(rgb.r, max(rgb.g, rgb.b));
                float minC = min(rgb.r, min(rgb.g, rgb.b));
                float value = maxC;
                float sat   = (maxC - minC) / max(maxC, 1e-5);

                float redDominance = rgb.r - max(rgb.g, rgb.b); // how much r is above others

                float redDomMask = saturate((redDominance - _RedDominanceMin) * 10.0);
                float satMask    = saturate((sat          - _RedSaturationMin) * 5.0);
                float valMask    = saturate((value       - _RedValueMin)      * 5.0);

                float preserveMask = redDomMask * satMask * valMask;
                preserveMask *= _PreserveRedStrength;

                float3 bwRGB = lerp(grayRGB, rgb, preserveMask);

                // NEW: 效果强度 0..1，0 时直接用原图 rgb
                float3 finalRGB = lerp(rgb, bwRGB, _EffectIntensity);

                return float4(finalRGB, 1.0);
            }
            ENDHLSL
        }
    }
}
