Shader "Hidden/GRKingRP/PostProcessing/Bloom"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZTest Always
        ZWrite Off
        Cull Off

        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"

            TEXTURE2D(_PostFXSource);
            TEXTURE2D(_BloomSource2);
            TEXTURE2D(_BloomHighlightSource);

            float4 _PostFXSource_TexelSize;

            // _PostFXSource：当前低分辨率输入。
            // _BloomSource2：Add/Scatter 时使用的高分辨率输入。
            bool _BloomBicubicUpsampling;
            float _BloomIntensity;

            // x: threshold
            // y: threshold * softKnee - threshold
            // z: 2 * threshold * softKnee
            // w: 0.25 / (threshold * softKnee + 0.00001)
            float4 _BloomThreshold;

            static const float2 BloomFirefliesOffsets[5] =
            {
                float2(0.0, 0.0),
                float2(-1.0, -1.0),
                float2(-1.0, 1.0),
                float2(1.0, -1.0),
                float2(1.0, 1.0)
            };

            static const float BloomHorizontalOffsets[9] =
            {
                -4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0
            };

            static const float BloomHorizontalWeights[9] =
            {
                0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
                0.19459459, 0.12162162, 0.05405405, 0.01621622
            };

            static const float BloomVerticalOffsets[5] =
            {
                -3.23076923, -1.38461538, 0.0, 1.38461538, 3.23076923
            };

            static const float BloomVerticalWeights[5] =
            {
                0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027
            };

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

            float4 GetSource2(float2 screenUV)
            {
                return SAMPLE_TEXTURE2D(_BloomSource2, sampler_LinearClamp, screenUV);
            }

            float4 GetSourceBicubic(float2 screenUV)
            {
                return SampleTexture2DBicubic(
                    TEXTURE2D_ARGS(_PostFXSource, sampler_LinearClamp),
                    screenUV,
                    _PostFXSource_TexelSize.zwxy,
                    1.0,
                    0);
            }

            float4 GetSourceTexelSize()
            {
                return _PostFXSource_TexelSize;
            }

            // 1. 预过滤
            // 应用阈值，保留高亮部分，并通过 soft knee 平滑阈值边界。
            float3 ApplyBloomThreshold(float3 color)
            {
                float brightness = Max3(color.r, color.g, color.b);
                float soft = brightness + _BloomThreshold.y;
                soft = clamp(soft, 0.0, _BloomThreshold.z);
                soft = soft * soft * _BloomThreshold.w;
                float contribution = max(soft, brightness - _BloomThreshold.x);
                contribution /= max(brightness, 0.00001);
                return color * contribution;
            }

            float4 BloomPrefilterPassFragment(Varyings input) : SV_Target
            {
                float3 color = ApplyBloomThreshold(GetSource(input.screenUV).rgb);
                return float4(color, 1.0);
            }

            float4 BloomPrefilterFirefliesPassFragment(Varyings input) : SV_Target
            {
                float3 color = 0.0;
                float weightSum = 0.0;

                // 采样更大区域；像素越亮，权重越低，抑制孤立高亮点闪烁。
                UNITY_UNROLL
                for (int i = 0; i < 5; i++)
                {
                    float3 sampleColor = GetSource(
                        input.screenUV + BloomFirefliesOffsets[i] *
                        GetSourceTexelSize().xy * 2.0).rgb;
                    sampleColor = ApplyBloomThreshold(sampleColor);
                    float weight = 1.0 / (Luminance(sampleColor) + 1.0);
                    color += sampleColor * weight;
                    weightSum += weight;
                }

                color /= max(weightSum, 0.00001);
                return float4(color, 1.0);
            }

            // 2. 模糊
            float4 BloomHorizontalPassFragment(Varyings input) : SV_Target
            {
                float3 color = 0.0;
                // 水平模糊的输出通常会降到一半宽度，所以源纹素偏移乘 2。
                UNITY_UNROLL
                for (int i = 0; i < 9; i++)
                {
                    float offset = BloomHorizontalOffsets[i] * 2.0 * GetSourceTexelSize().x;
                    color += GetSource(input.screenUV + float2(offset, 0.0)).rgb *
                        BloomHorizontalWeights[i];
                }
                return float4(color, 1.0);
            }

            float4 BloomVerticalPassFragment(Varyings input) : SV_Target
            {
                float3 color = 0.0;
                // 将相邻两次采样合并为一次双线性采样，共使用 5 个采样点。
                UNITY_UNROLL
                for (int i = 0; i < 5; i++)
                {
                    float offset = BloomVerticalOffsets[i] * GetSourceTexelSize().y;
                    color += GetSource(input.screenUV + float2(0.0, offset)).rgb *
                        BloomVerticalWeights[i];
                }
                return float4(color, 1.0);
            }

            // 3. 叠加
            float4 BloomAddPassFragment(Varyings input) : SV_Target
            {
                // Add 模式直接把低分辨率 Bloom 叠加到高分辨率输入。
                float3 lowRes;
                if (_BloomBicubicUpsampling)
                {
                    lowRes = GetSourceBicubic(input.screenUV).rgb;
                }
                else
                {
                    lowRes = GetSource(input.screenUV).rgb;
                }

                float4 highRes = GetSource2(input.screenUV);
                return float4(lowRes * _BloomIntensity + highRes.rgb, highRes.a);
            }

            float4 BloomScatterPassFragment(Varyings input) : SV_Target
            {
                float3 lowRes;
                if (_BloomBicubicUpsampling)
                {
                    lowRes = GetSourceBicubic(input.screenUV).rgb;
                }
                else
                {
                    lowRes = GetSource(input.screenUV).rgb;
                }

                float3 highRes = GetSource2(input.screenUV).rgb;
                // Scatter 模式在高、低分辨率 Bloom 之间插值。
                return float4(lerp(highRes, lowRes, _BloomIntensity), 1.0);
            }

            float4 BloomScatterFinalPassFragment(Varyings input) : SV_Target
            {
                float3 lowRes;
                if (_BloomBicubicUpsampling)
                {
                    lowRes = GetSourceBicubic(input.screenUV).rgb;
                }
                else
                {
                    lowRes = GetSource(input.screenUV).rgb;
                }

                float4 highRes = GetSource2(input.screenUV);
                // 只扣除参与 Bloom 的输入高亮；角色模式下背景不会被扣除。
                float3 highlightSource = SAMPLE_TEXTURE2D(
                    _BloomHighlightSource, sampler_LinearClamp, input.screenUV).rgb;
                lowRes += highRes.rgb - ApplyBloomThreshold(highlightSource);
                return float4(lerp(highRes.rgb, lowRes, _BloomIntensity), highRes.a);
            }


            float4 BloomCharacterCopyFragment(Varyings input) : SV_Target
            {
                return GetSource(input.screenUV);
            }
        ENDHLSL

        // Pass 0～1：预过滤（普通 / 抑制萤火虫，二选一）。
        // 后续 C# 中的 BloomPass 枚举需要与此处顺序保持一致。
        Pass
        {
            Name "Bloom Prefilter Pass"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BloomPrefilterPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Prefilter Fireflies Pass"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BloomPrefilterFirefliesPassFragment
            ENDHLSL
        }

        // Pass 2～3：水平 / 垂直模糊。
        Pass
        {
            Name "Bloom Horizontal Pass"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BloomHorizontalPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Vertical Pass"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BloomVerticalPassFragment
            ENDHLSL
        }

        // Pass 4～6：Add 叠加 / Scatter 层间混合 / Scatter 最终合成。
        Pass
        {
            Name "Bloom Add Pass"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BloomAddPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Scatter Pass"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BloomScatterPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Scatter Final Pass"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BloomScatterFinalPassFragment
            ENDHLSL
        }

        // Pass 7：只提取相机 Stencil 最低位为 1 的角色区域。
        Pass
        {
            Name "Bloom Character Copy Pass"
            Stencil
            {
                Ref 1
                ReadMask 1
                Comp Equal
                Pass Keep
                Fail Keep
                ZFail Keep
            }
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BloomCharacterCopyFragment
            ENDHLSL
        }

    }

    Fallback Off
}
