Shader "ShaderLab/WipeSmear"
{
    Properties
    {
        // 0–1: how far the wipe has travelled along the zig-zag path
        _WipeProgress   ("Wipe Progress", Range(0,1)) = 0.0

        // Max smear distance in pixels along the brush direction
        _SmearDistance  ("Smear Distance (px)", Range(0, 80)) = 30

        // Width of the “wipe band” along the path (small = sharp, large = soft)
        _SmearWidth     ("Smear Width (0-1 along path)", Range(0.02, 0.6)) = 0.25
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
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                // x,y = 1/width,1/height  z,w = width,height
                float4 _BlitTexture_TexelSize;

                float  _WipeProgress;   // 0..1 along the path
                float  _SmearDistance;  // smear length in pixels
                float  _SmearWidth;     // 0..1 thickness of the active band
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

            // Full-screen triangle
            Interpolators vert (MeshData v)
            {
                Interpolators o;
                o.posCS = GetFullScreenTriangleVertexPosition(v.vertexID);
                o.uv    = GetFullScreenTriangleTexCoord   (v.vertexID);
                return o;
            }

            // Project point p onto segment Pa->Pb
            void ProjectToSegment(float2 p, float2 Pa, float2 Pb,
                                  out float sAlong, out float dist)
            {
                float2 seg    = Pb - Pa;
                float  segLen = max(length(seg), 1e-4);
                float2 dir    = seg / segLen;

                float2 v  = p - Pa;
                float  t  = dot(v, dir) / segLen;   // along-segment
                t         = saturate(t);

                float2 q  = Pa + dir * (segLen * t);
                dist      = length(p - q);
                sAlong    = t;                      // 0..1 along this segment
            }

            float4 frag (Interpolators i) : SV_Target
            {
                float2 uv = i.uv;

                // Original scene color
                float3 baseCol = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv).rgb;

                // Zig-zag path control points (slightly outside 0–1 UV)
                const float2 P0 = float2( 1.30,  1.10); // top-right
                const float2 P1 = float2(-0.05,  0.65); // left-middle
                const float2 P2 = float2( 1.10,  0.30); // right-middle-lower
                const float2 P3 = float2(-0.10, -0.40); // bottom-left

                float2 S0 = P1 - P0;
                float2 S1 = P2 - P1;
                float2 S2 = P3 - P2;

                float len0 = length(S0);
                float len1 = length(S1);
                float len2 = length(S2);

                float totalLen = max(len0 + len1 + len2, 1e-4);

                // Project this pixel onto each segment
                float s0, d0;
                float s1, d1;
                float s2, d2;

                ProjectToSegment(uv, P0, P1, s0, d0);
                ProjectToSegment(uv, P1, P2, s1, d1);
                ProjectToSegment(uv, P2, P3, s2, d2);

                // Distance along full path if we were on each segment
                float path0 = (0.0          + len0 * s0);
                float path1 = (len0         + len1 * s1);
                float path2 = (len0 + len1  + len2 * s2);

                float sPath = 0.0;           // 0..1 along whole polyline
                float2 dir  = float2(-1, 0); // fallback direction

                // Choose segment with smallest distance
                if (d0 <= d1 && d0 <= d2)
                {
                    sPath = path0 / totalLen;
                    dir   = normalize(S0);
                }
                else if (d1 <= d0 && d1 <= d2)
                {
                    sPath = path1 / totalLen;
                    dir   = normalize(S1);
                }
                else
                {
                    sPath = path2 / totalLen;
                    dir   = normalize(S2);
                }

                float progress = saturate(_WipeProgress);
                float width    = max(_SmearWidth, 1e-4);

                // How far “behind the brush” this pixel is
                float behind = saturate((progress - sPath) / width);

                // Not reached by the wipe yet
                if (behind <= 0.0)
                    return float4(baseCol, 1.0);

                // Random factor per pixel so streaks vary in length
                float noise = frac(sin(dot(uv, float2(123.4, 456.7))) * 43758.5453);
                float randomFactor = lerp(0.4, 1.4, noise);

                float pixelScale = _BlitTexture_TexelSize.x; // ~1 / width
                float maxDistUV  = _SmearDistance * pixelScale * randomFactor;

                float smearDist = maxDistUV * behind;

                // Sample along the path (against dir) to create a connected streak
                const int STEPS = 12;
                float3 accum   = 0.0;
                float  totalW  = 0.0;

                float2 smearDir = -normalize(dir);

                [unroll]
                for (int s = 0; s < STEPS; s++)
                {
                    float t = (float)s / (float)(STEPS - 1); // 0..1 along smear
                    float w = 1.0 - t;                       // heavier near current pixel

                    float dist    = smearDist * t;
                    float2 sampleUV = uv + smearDir * dist;
                    sampleUV = clamp(sampleUV, 0.0, 1.0);

                    float3 c = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, sampleUV).rgb;
                    accum   += c * w;
                    totalW  += w;
                }

                float3 smearCol = accum / max(totalW, 1e-4);

                // Lerp between original and smeared color based on how far behind it is
                float3 finalCol = lerp(baseCol, smearCol, behind);

                return float4(finalCol, 1.0);
            }
            ENDHLSL
        }
    }
}
