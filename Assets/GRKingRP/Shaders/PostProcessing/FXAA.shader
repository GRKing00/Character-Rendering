Shader "Hidden/GRKingRP/PostProcessing/FXAA"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZTest Always
        ZWrite Off
        Cull Off

        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"

            TEXTURE2D(_PostFXSource);

            float4 _PostFXSource_TexelSize;

            // x: fixed threshold
            // y: relative threshold
            // z: subpixel blending
            // w: unused
            float4 _FXAAConfig;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 screenUV : VAR_SCREEN_UV;
            };

            Varyings DefaultPassVertex(uint vertexID : SV_VertexID)
            {
                Varyings output;
                output.positionCS = float4(
                    vertexID <= 1 ? -1.0 : 3.0,
                    vertexID == 1 ? 3.0 : -1.0,
                    0.0, 1.0
                );
                output.screenUV = float2(
                    vertexID <= 1 ? 0.0 : 2.0,
                    vertexID == 1 ? 2.0 : 0.0
                );

                if (_ProjectionParams.x < 0.0)
                {
                    output.screenUV.y = 1.0 - output.screenUV.y;
                }

                return output;
            }

            float4 GetSource(float2 screenUV)
            {
                return SAMPLE_TEXTURE2D(_PostFXSource, sampler_LinearClamp, screenUV);
            }

            // 当前后处理链没有把亮度写入 Alpha，因此使用绿色通道近似亮度。
            // 绿色通道最接近人眼感知亮度中权重最高的分量，也能避免额外的点积计算。
            float GetLuma(float2 screenUV, float uOffset = 0.0, float vOffset = 0.0)
            {
                screenUV += float2(uOffset, vOffset) * _PostFXSource_TexelSize.xy;
                return GetSource(screenUV).g;
            }

            struct LumaNeighborhood
            {
                float m;
                float n;
                float e;
                float s;
                float w;
                float ne;
                float se;
                float sw;
                float nw;
                float highest;
                float lowest;
                float range;
            };

            LumaNeighborhood GetLumaNeighborhood(float2 screenUV)
            {
                LumaNeighborhood luma;
                luma.m = GetLuma(screenUV);
                luma.n = GetLuma(screenUV, 0.0, 1.0);
                luma.e = GetLuma(screenUV, 1.0, 0.0);
                luma.s = GetLuma(screenUV, 0.0, -1.0);
                luma.w = GetLuma(screenUV, -1.0, 0.0);
                luma.ne = GetLuma(screenUV, 1.0, 1.0);
                luma.se = GetLuma(screenUV, 1.0, -1.0);
                luma.sw = GetLuma(screenUV, -1.0, -1.0);
                luma.nw = GetLuma(screenUV, -1.0, 1.0);

                luma.highest = max(max(max(max(luma.m, luma.n), luma.e), luma.s), luma.w);
                luma.lowest = min(min(min(min(luma.m, luma.n), luma.e), luma.s), luma.w);
                luma.range = luma.highest - luma.lowest;
                return luma;
            }

            bool CanSkipFXAA(LumaNeighborhood luma)
            {
                // 同时使用固定阈值与相对阈值，避免在平坦区域浪费边缘搜索。
                return luma.range < max(_FXAAConfig.x, _FXAAConfig.y * luma.highest);
            }

            bool IsHorizontalEdge(LumaNeighborhood luma)
            {
                float horizontal =
                    2.0 * abs(luma.n + luma.s - 2.0 * luma.m) +
                    abs(luma.ne + luma.se - 2.0 * luma.e) +
                    abs(luma.nw + luma.sw - 2.0 * luma.w);
                float vertical =
                    2.0 * abs(luma.e + luma.w - 2.0 * luma.m) +
                    abs(luma.ne + luma.nw - 2.0 * luma.n) +
                    abs(luma.se + luma.sw - 2.0 * luma.s);
                return horizontal >= vertical;
            }

            struct FXAAEdge
            {
                bool isHorizontal;
                float pixelStep;
                float lumaGradient;
                float otherLuma;
            };

            FXAAEdge GetFXAAEdge(LumaNeighborhood luma)
            {
                FXAAEdge edge;
                edge.isHorizontal = IsHorizontalEdge(luma);

                float lumaP;
                float lumaN;
                if (edge.isHorizontal)
                {
                    edge.pixelStep = _PostFXSource_TexelSize.y;
                    lumaP = luma.n;
                    lumaN = luma.s;
                }
                else
                {
                    edge.pixelStep = _PostFXSource_TexelSize.x;
                    lumaP = luma.e;
                    lumaN = luma.w;
                }

                float gradientP = abs(lumaP - luma.m);
                float gradientN = abs(lumaN - luma.m);
                if (gradientP < gradientN)
                {
                    edge.pixelStep = -edge.pixelStep;
                    edge.lumaGradient = gradientN;
                    edge.otherLuma = lumaN;
                }
                else
                {
                    edge.lumaGradient = gradientP;
                    edge.otherLuma = lumaP;
                }

                return edge;
            }

            float GetSubpixelBlendFactor(LumaNeighborhood luma)
            {
                float filter = 2.0 * (luma.n + luma.e + luma.s + luma.w);
                filter += luma.ne + luma.nw + luma.se + luma.sw;
                filter *= 1.0 / 12.0;
                filter = abs(filter - luma.m);
                filter = saturate(filter / max(luma.range, 0.00001));
                filter = smoothstep(0.0, 1.0, filter);
                return filter * filter * _FXAAConfig.z;
            }

            #if defined(FXAA_QUALITY_LOW)
                #define EXTRA_EDGE_STEPS 3
                #define EDGE_STEP_SIZES 1.5, 2.0, 2.0
                #define LAST_EDGE_STEP_GUESS 8.0
            #elif defined(FXAA_QUALITY_MEDIUM)
                #define EXTRA_EDGE_STEPS 8
                #define EDGE_STEP_SIZES 1.5, 2.0, 2.0, 2.0, 2.0, 2.0, 2.0, 4.0
                #define LAST_EDGE_STEP_GUESS 8.0
            #else
                #define EXTRA_EDGE_STEPS 10
                #define EDGE_STEP_SIZES 1.0, 1.0, 1.0, 1.0, 1.5, 2.0, 2.0, 2.0, 2.0, 4.0
                #define LAST_EDGE_STEP_GUESS 8.0
            #endif

            static const float EdgeStepSizes[EXTRA_EDGE_STEPS] = { EDGE_STEP_SIZES };

            float GetEdgeBlendFactor(LumaNeighborhood luma, FXAAEdge edge, float2 screenUV)
            {
                float2 edgeUV = screenUV;
                float2 uvStep = 0.0;
                if (edge.isHorizontal)
                {
                    edgeUV.y += 0.5 * edge.pixelStep;
                    uvStep.x = _PostFXSource_TexelSize.x;
                }
                else
                {
                    edgeUV.x += 0.5 * edge.pixelStep;
                    uvStep.y = _PostFXSource_TexelSize.y;
                }

                float edgeLuma = 0.5 * (luma.m + edge.otherLuma);
                float gradientThreshold = 0.25 * edge.lumaGradient;

                float2 uvP = edgeUV + uvStep;
                float lumaDeltaP = GetLuma(uvP) - edgeLuma;
                bool atEndP = abs(lumaDeltaP) >= gradientThreshold;

                int i;
                UNITY_UNROLL
                for (i = 0; i < EXTRA_EDGE_STEPS && !atEndP; i++)
                {
                    uvP += uvStep * EdgeStepSizes[i];
                    lumaDeltaP = GetLuma(uvP) - edgeLuma;
                    atEndP = abs(lumaDeltaP) >= gradientThreshold;
                }
                if (!atEndP)
                {
                    uvP += uvStep * LAST_EDGE_STEP_GUESS;
                }

                float2 uvN = edgeUV - uvStep;
                float lumaDeltaN = GetLuma(uvN) - edgeLuma;
                bool atEndN = abs(lumaDeltaN) >= gradientThreshold;

                UNITY_UNROLL
                for (i = 0; i < EXTRA_EDGE_STEPS && !atEndN; i++)
                {
                    uvN -= uvStep * EdgeStepSizes[i];
                    lumaDeltaN = GetLuma(uvN) - edgeLuma;
                    atEndN = abs(lumaDeltaN) >= gradientThreshold;
                }
                if (!atEndN)
                {
                    uvN -= uvStep * LAST_EDGE_STEP_GUESS;
                }

                float distanceToEndP;
                float distanceToEndN;
                if (edge.isHorizontal)
                {
                    distanceToEndP = uvP.x - screenUV.x;
                    distanceToEndN = screenUV.x - uvN.x;
                }
                else
                {
                    distanceToEndP = uvP.y - screenUV.y;
                    distanceToEndN = screenUV.y - uvN.y;
                }

                float distanceToNearestEnd;
                bool deltaSign;
                if (distanceToEndP <= distanceToEndN)
                {
                    distanceToNearestEnd = distanceToEndP;
                    deltaSign = lumaDeltaP >= 0.0;
                }
                else
                {
                    distanceToNearestEnd = distanceToEndN;
                    deltaSign = lumaDeltaN >= 0.0;
                }

                if (deltaSign == (luma.m - edgeLuma >= 0.0))
                {
                    return 0.0;
                }

                return 0.5 - distanceToNearestEnd / max(distanceToEndP + distanceToEndN, 0.00001);
            }

            float4 FXAAPassFragment(Varyings input) : SV_Target
            {
                LumaNeighborhood luma = GetLumaNeighborhood(input.screenUV);
                if (CanSkipFXAA(luma))
                {
                    return GetSource(input.screenUV);
                }

                FXAAEdge edge = GetFXAAEdge(luma);
                float blendFactor = max(
                    GetSubpixelBlendFactor(luma),
                    GetEdgeBlendFactor(luma, edge, input.screenUV)
                );

                float2 blendUV = input.screenUV;
                if (edge.isHorizontal)
                {
                    blendUV.y += blendFactor * edge.pixelStep;
                }
                else
                {
                    blendUV.x += blendFactor * edge.pixelStep;
                }

                return GetSource(blendUV);
            }

        ENDHLSL

        Pass
        {
            Name "FXAA Pass"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment FXAAPassFragment
                #pragma multi_compile_local_fragment _ FXAA_QUALITY_LOW FXAA_QUALITY_MEDIUM
            ENDHLSL
        }

    }

    Fallback Off
}
