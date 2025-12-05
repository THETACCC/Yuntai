Shader "ShaderLab/SpiderWeb"
{
    Properties
    {
        _WebColor    ("Web Color", Color) = (1,1,1,1)
        _WebOpacity  ("Web Opacity", Range(0,1)) = 0.8

        // Web center in screen UV (0-1)
        _CenterUV    ("Center (UV)", Vector) = (0.5, 0.5, 0, 0)

        // Radial spokes
        _SpokeCount  ("Spoke Count", Range(4, 40)) = 14
        _SpokePixels ("Spoke Thickness (px)", Range(0.3, 4)) = 1.0

        // Curved rings
        _RingCount   ("Ring Count", Range(2, 24)) = 8
        _RingPixels  ("Ring Thickness (px)", Range(0.3, 4)) = 1.0

        // How much rings are denser near the center (<1 = more dense)
        _CenterDensityExp ("Center Density Exponent", Range(0.3, 3)) = 0.7

        // Sag of rings between spokes (0 = perfect circle)
        _SagAmount   ("Sag Amount", Range(0, 0.4)) = 0.08

        // Radius of web in UV space (in aspect-corrected units)
        _MaxRadius   ("Max Radius", Range(0.1, 2.0)) = 0.8

        // Fraction (0-1) of radius where rings exist (outer part = spokes only)
        _RingMaxRadius ("Ring Max Radius", Range(0.2, 1.0)) = 0.75

        // Fade out near edge of web
        _EdgeFade    ("Edge Fade", Range(0, 1)) = 0.3

        // build animation
        _BuildProgress ("Build Progress", Range(0,1)) = 1.0
        _BuildFeather  ("Build Feather",  Range(0.0,0.25)) = 0.03
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

            #define PI 3.14159265
            #define MAX_RINGS  32
            #define MAX_SPOKES 64

            CBUFFER_START(UnityPerMaterial)
                float4 _BlitTexture_TexelSize;

                float4 _WebColor;
                float  _WebOpacity;

                float4 _CenterUV;

                float  _SpokeCount;
                float  _SpokePixels;

                float  _RingCount;
                float  _RingPixels;

                float  _CenterDensityExp;
                float  _SagAmount;

                float  _MaxRadius;
                float  _RingMaxRadius;
                float  _EdgeFade;

                float  _BuildProgress;
                float  _BuildFeather;
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

            float4 frag (Interpolators i) : SV_Target
            {
                float2 uv = i.uv;
                float4 sceneCol = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv);

                // aspect-corrected coordinates from center
                float2 d = uv - _CenterUV.xy;

                //stretch X so circles don’t get squished by screen aspect
                float aspect = _BlitTexture_TexelSize.z / _BlitTexture_TexelSize.w; // width/height
                float2 dWeb = float2(d.x * aspect, d.y);

                float r = length(dWeb);
                //If outside maxRadius, don’t draw web but the original scene color
                if (r > _MaxRadius)
                    return sceneCol;

                // angle [-PI, PI] is mapped to [0,1] around the circle
                float angle = atan2(dWeb.y, dWeb.x);
                float angle01 = angle / (2.0 * PI) + 0.5;

                // normalized radius & normalized position
                float rNorm = r / _MaxRadius; // center=0, outer web radius=1
                float2 pNorm = dWeb / _MaxRadius; // radius <= 1

                // convert pixel thickness to "radius units"
                float pixelUV = _BlitTexture_TexelSize.x; // ~1/width
                float spokeWidth = (_SpokePixels * pixelUV) / max(_MaxRadius, 1e-4);
                float ringWidth  = (_RingPixels  * pixelUV) / max(_MaxRadius, 1e-4);

                float buildP  = saturate(_BuildProgress); //0 web invisible, 1 fully drawn
                float buildFeather = max(_BuildFeather, 1e-4); //how smoothly new segments appear

                // radial lines:
                float minSpokeDist = 1e5;
                int spokeCount = (int)_SpokeCount;
                spokeCount = clamp(spokeCount, 1, MAX_SPOKES);

                [unroll]
                for (int s = 0; s < MAX_SPOKES; s++)
                {
                    if (s >= spokeCount)
                        break;

                    float ang = (2.0 * PI * (float)s) / max(_SpokeCount, 1.0);
                    float2 dir = float2(cos(ang), sin(ang));// unit direction of this spoke

                    // distance from point to infinite line through origin along dir
                    float proj = dot(pNorm, dir);
                    float2 closest = dir * proj;
                    float2 diff = pNorm - closest;
                    float dist = length(diff);

                    minSpokeDist = min(minSpokeDist, dist);
                }

                float spokeMask = smoothstep(spokeWidth, 0.0, minSpokeDist);

                // build timing for spokes: center first, outer later
                float spokeBirth  = rNorm; // 0 at center, 1 at edge
                float spokeVisible = saturate( (buildP - spokeBirth) / buildFeather );
                spokeMask *= spokeVisible;

                // sectorT: 0 at one spoke, 1 at next spoke (for ring sag / segment timing)
                float sectorCoord = angle01 * _SpokeCount;
                float sectorT     = frac(sectorCoord);

                //rings:
                float minRingDist   = 1e5;
                int   ringCount     = (int)_RingCount;
                ringCount = clamp(ringCount, 0, MAX_RINGS);
                float ringMax       = saturate(_RingMaxRadius); // no rings beyond this fraction

                // inner dense / middle loose / outer normal
                float innerRegion = ringMax * 0.35;
                float midRegion   = ringMax * 0.75;

                int innerCount = max(1, ringCount / 2);
                int outerCount = max(0, ringCount - innerCount);

                float nearestRingIdx = -1.0;

                [unroll]
                for (int k = 0; k < MAX_RINGS; k++)
                {
                    if (k >= ringCount)
                        break;

                    float targetRadius = 0.0;

                    if (k < innerCount)
                    {
                        // inner dense group in [0, innerRegion]
                        float f = (float)(k + 1) / (float)(innerCount + 1); // 0..1
                        float base = pow(f, _CenterDensityExp);
                        targetRadius = base * innerRegion;
                    }
                    else
                    {
                        // middle + outer group in [innerRegion, ringMax]
                        int idxOuter = k - innerCount;
                        float g = (float)(idxOuter + 1) / (float)(outerCount + 1 + 1e-4); // 0..1

                        // exponent >1 -> looser in middle, a bit denser near outer edge
                        float baseMidOuter = pow(g, 1.5);

                        float span = max(ringMax - innerRegion, 1e-4);
                        targetRadius = innerRegion + baseMidOuter * span;
                    }

                    // sag between spokes: 0 at spokes, max at middle
                    float sag = _SagAmount * sin(sectorT * PI);
                    targetRadius *= (1.0 - sag);

                    float dist = abs(rNorm - targetRadius);
                    if (dist < minRingDist)
                    {
                        minRingDist   = dist;
                        nearestRingIdx = (float)k;
                    }
                }

                float ringMask = smoothstep(ringWidth, 0.0, minRingDist);

                // build timing for rings: inner rings first, then outer; within ring,
                // go from one spoke to the next (small segments).
                float ringVisible = 1.0;
                if (ringCount > 0 && nearestRingIdx >= 0.0)
                {
                    float ringStep = 1.0 / (ringCount + 1); // radial step
                    float ringBase = nearestRingIdx * ringStep; // this ring's base time
                    // add sectorT so we build along the arc segment-by-segment
                    float ringBirth = ringBase + sectorT * ringStep; // 0..1

                    ringVisible = saturate( (buildP - ringBirth) / buildFeather );
                }
                ringMask *= ringVisible;

                // combine
                float webMask = saturate(spokeMask + ringMask);

                // fade at outer radius
                if (_EdgeFade > 0.0)
                {
                    float fadeStart = _MaxRadius * (1.0 - _EdgeFade);
                    float fade = saturate((fadeStart - r) / max(0.0001, fadeStart - _MaxRadius));
                    webMask *= fade;
                }

                float alpha = webMask * _WebOpacity;
                if (alpha <= 0.001)
                    return sceneCol;

                //alpha 0 scene
                //alpha 1 web
                float3 webColor = _WebColor.rgb;
                float3 finalRGB = lerp(sceneCol.rgb, webColor, alpha);

                return float4(finalRGB, sceneCol.a);
            }
            ENDHLSL
        }
    }
}
