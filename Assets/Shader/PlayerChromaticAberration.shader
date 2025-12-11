Shader "ShaderLab/PlayerChromaticAberration"
{
    Properties
    {
        // Base sprite texture
        _MainTex ("Sprite Texture", 2D) = "white" {}

        // Overall tint color (multiplies the sampled sprite color)
        _Color   ("Tint", Color) = (1,1,1,1)

        // Maximum distance the “ghost” copies can move away from the main sprite in UV space
        _MaxOffset ("Max Offset", Range(0, 0.2)) = 0.18

        // 0 = all channels stacked together, 1 = fully separated ghosts
        // This is animated by script to drive the soul-separation effect.
        _Phase ("Phase (0-1)", Range(0, 1)) = 0
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

                float  _MaxOffset;
                float  _Phase;
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

            // Standard sprite vertex: transform to clip space and pass through UV + vertex color
            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv   = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;   // multiply vertex color with material tint
                return o;
            }

            float4 frag (Varyings i) : SV_Target
            {
                float2 uv = i.uv;

                // Center sample (no offset)
                float4 colCenter = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                // If this pixel is fully transparent, early out
                if (colCenter.a <= 0.001)
                    return float4(0,0,0,0);

                // Phase decides how far the channels separate
                float phase     = saturate(_Phase);
                float offsetMag = _MaxOffset * phase;

                // Direction in texture space along which we separate the ghosts.
                // Here we use local X (left/right).
                float2 dir = float2(1.0, 0.0);

                // Front (right) ghost sample
                float2 uvFront = saturate(uv + dir * offsetMag);  // clamp to [0,1]
                float4 colFront = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvFront);

                // Back (left) ghost sample
                float2 uvBack  = saturate(uv - dir * offsetMag);  // clamp to [0,1]
                float4 colBack = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvBack);

                // Stylized channel weighting:
                //  - center is slightly darkened
                //  - right ghost is boosted and tinted red
                //  - left ghost is boosted and tinted cyan/blue
                float3 cCenter = colCenter.rgb * 0.3;
                float3 cFront  = colFront.rgb  * float3(2.0, 0.3, 0.3);
                float3 cBack   = colBack.rgb   * float3(0.3, 1.8, 2.0);

                float aCenter = colCenter.a;
                float aFront  = colFront.a;
                float aBack   = colBack.a;

                // Accumulate RGB weighted by alpha so overlapping ghosts blend nicely
                float3 accumRGB = cCenter * aCenter + cFront * aFront + cBack * aBack;
                float  accumA   = max(aCenter, max(aFront, aBack));

                // Normalize by the sum of alphas so brightness stays reasonable
                if (aCenter + aFront + aBack > 0.0001)
                    accumRGB /= (aCenter + aFront + aBack + 1e-5);

                // Apply vertex + material tint
                accumRGB *= i.color.rgb;
                float outA = accumA * i.color.a;

                return float4(accumRGB, outA);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
