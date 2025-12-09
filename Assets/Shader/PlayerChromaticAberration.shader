Shader "ShaderLab/PlayerChromaticAberration"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color   ("Tint", Color) = (1,1,1,1)

        // 控制残影离本体多远
        _MaxOffset ("Max Offset", Range(0, 0.2)) = 0.18

        // 0 = 合在一起，1 = 最大分离（脚本改 _Phase）
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

                // 中心像素
                float4 colCenter = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                if (colCenter.a <= 0.001)
                    return float4(0,0,0,0);

                float phase     = saturate(_Phase);
                float offsetMag = _MaxOffset * phase;

                // 残影方向：贴图局部X轴（左右）
                float2 dir = float2(1.0, 0.0);

                // 右边 echo
                float2 uvFront = saturate(uv + dir * offsetMag);  // clamp 到 [0,1]
                float4 colFront = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvFront);

                // 左边 echo
                float2 uvBack  = saturate(uv - dir * offsetMag);  // clamp 到 [0,1]
                float4 colBack = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvBack);

                // 夸张的颜色方便肉眼看出左右
                float3 cCenter = colCenter.rgb * 0.3;                         // 中间稍微压暗
                float3 cFront  = colFront.rgb  * float3(2.0, 0.3, 0.3);       // 右侧明显偏红
                float3 cBack   = colBack.rgb   * float3(0.3, 1.8, 2.0);       // 左侧明显偏青

                float aCenter = colCenter.a;
                float aFront  = colFront.a;
                float aBack   = colBack.a;

                float3 accumRGB = cCenter * aCenter + cFront * aFront + cBack * aBack;
                float  accumA   = max(aCenter, max(aFront, aBack));

                if (aCenter + aFront + aBack > 0.0001)
                    accumRGB /= (aCenter + aFront + aBack + 1e-5);

                accumRGB *= i.color.rgb;
                float outA = accumA * i.color.a;

                return float4(accumRGB, outA);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
