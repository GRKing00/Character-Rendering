Shader "Hidden/GRKingRP/PostProcessing/ToneMapping"
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
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"

            TEXTURE2D(_PostFXSource);

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
            
            float4 _NaesParams;
            float4 _GTParams1;
            float4 _GTParams2;
            float4 _FilmParams1;
            float4 _FilmParams2;
            
            half3 NAESTonemap(half3 input)
            {
                 // input=(1.36*input+0.047)*input/((0.93*input+0.56)*input+0.14);
                 input=(_NaesParams.x * input + _NaesParams.y)*input/((_NaesParams.z*input+_NaesParams.w)*input+0.14);
                 return input;
            }
            
            //NAES ToneMapping
            half4 NAESTonemapFrag (Varyings input) : SV_Target
            {
                float4 color = GetSource(input.screenUV);
                return half4(NAESTonemap(color.rgb), color.a);
            }
            
            float W_f(float x, float e0, float e1)
            {
                if (x <= e0)
                    return 0;
                if (x >= e1)
                    return 1;
                float a = (x - e0) / (e1 - e0);
                return a * a * (3 - 2 * a);
            }
            
            float H_f(float x, float e0, float e1)
            {
                if (x <= e0)
                    return 0;
                if (x >= e1)
                    return 1;
                return (x - e0) / (e1 - e0 + FLT_EPS);
            }
            
            float GranTurismoTonemap(float x)
            {
                // float P = 1; // Maximum brightness
                // float a = 1; // Contrast
                // float m = 0.22; // Linear section start
                // float l = 0.4; // Linear section length
                // float c = 1.33; // Black pow  def 1 
                // float b = 0; // Black min
                float P = _GTParams1.x; // Maximum brightness
                float a = _GTParams1.y; // Contrast
                float m = _GTParams1.z; // Linear section start
                float l = _GTParams1.w; // Linear section length
                float c = _GTParams2.x; // Black pow  def 1 
                float b = _GTParams2.y; // Black min
                float l0 = (P - m) * l / a;
                float L0 = m - m / a;
                float L1 = m + (1 - m) / a;
                float L_x = m + a * (x - m);
                float T_x = m * pow(saturate(x / m), c) + b;
                float S0 = m + l0;
                float S1 = m + a * l0;
                float C2 = a * P / (P - S1);
                float S_x = P - (P - S1) * exp(-(C2 * (x - S0) / P));
                float w0_x = 1 - W_f(x, 0, m);
                float w2_x = H_f(x, m + l0, m + l0);
                float w1_x = 1 - w0_x - w2_x;
                float f_x = T_x * w0_x + L_x * w1_x + S_x * w2_x;
                return f_x;
            }
            
            //GT ToneMapping
            half4 GTTomemapFrag (Varyings input) : SV_Target
            {
                float4 color = GetSource(input.screenUV);
                color.r = GranTurismoTonemap(color.r);
                color.g = GranTurismoTonemap(color.g);
                color.b = GranTurismoTonemap(color.b);
                return color;
            }
        
            #define _FilmSlope _FilmParams1.x
            #define _FilmToe _FilmParams1.y
            #define _FilmShoulder _FilmParams1.z
            #define _FilmBlackClip _FilmParams2.x
            #define _FilmWhiteClip _FilmParams2.y
            // Ported from UE
            float3 FilmicACESTonemap(float3 aces)
            {
                // "Glow" module constants
                const float RRT_GLOW_GAIN = 0.05;
                const float RRT_GLOW_MID = 0.08;
             
                float saturation = rgb_2_saturation(aces);
                float ycIn = rgb_2_yc(aces);
                float s = sigmoid_shaper((saturation - 0.4) / 0.2);
                float addedGlow = 1.0 + glow_fwd(ycIn, RRT_GLOW_GAIN * s, RRT_GLOW_MID);
                aces *= addedGlow;
             
                const float RRT_RED_SCALE = 0.82;
                const float RRT_RED_PIVOT = 0.03;
                const float RRT_RED_HUE = 0.0;
                const float RRT_RED_WIDTH = 135.0;
             
                // --- Red modifier --- //
                float hue = rgb_2_hue(aces);
                float centeredHue = center_hue(hue, RRT_RED_HUE);
                float hueWeight;
                {
                    hueWeight = smoothstep(0.0, 1.0, 1.0 - abs(2.0 * centeredHue / RRT_RED_WIDTH));
                    hueWeight *= hueWeight;
                }
                //float hueWeight = Square( smoothstep(0.0, 1.0, 1.0 - abs(2.0 * centeredHue / RRT_RED_WIDTH)) );
             
                aces.r += hueWeight * saturation * (RRT_RED_PIVOT - aces.r) * (1.0 - RRT_RED_SCALE);
             
                // Use ACEScg primaries as working space
                float3 acescg = max(0.0, ACES_to_ACEScg(aces));
             
                // Pre desaturate
                acescg = lerp(dot(acescg, AP1_RGB2Y).xxx, acescg, 0.96);
             
                const half ToeScale = 1 + _FilmBlackClip - _FilmToe;
                const half ShoulderScale = 1 + _FilmWhiteClip - _FilmShoulder;
             
                const float InMatch = 0.18;
                const float OutMatch = 0.18;
             
                float ToeMatch;
                if (_FilmToe > 0.8)
                {
                    // 0.18 will be on straight segment
                    ToeMatch = (1 - _FilmToe - OutMatch) / _FilmSlope + log10(InMatch);
                }
                else
                {
                    // 0.18 will be on toe segment
             
                    // Solve for ToeMatch such that input of InMatch gives output of OutMatch.
                    const float bt = (OutMatch + _FilmBlackClip) / ToeScale - 1;
                    ToeMatch = log10(InMatch) - 0.5 * log((1 + bt) / (1 - bt)) * (ToeScale / _FilmSlope);
                }
             
                float StraightMatch = (1 - _FilmToe) / _FilmSlope - ToeMatch;
                float ShoulderMatch = _FilmShoulder / _FilmSlope - StraightMatch;
             
                half3 LogColor = log10(acescg);
                half3 StraightColor = _FilmSlope * (LogColor + StraightMatch);
             
                half3 ToeColor = (-_FilmBlackClip) + (2 * ToeScale) / (1 + exp((-2 * _FilmSlope / ToeScale) * (LogColor - ToeMatch)));
                half3 ShoulderColor = (1 + _FilmWhiteClip) - (2 * ShoulderScale) / (1 + exp((2 * _FilmSlope / ShoulderScale) * (LogColor - ShoulderMatch)));
             
                ToeColor = LogColor < ToeMatch ? ToeColor : StraightColor;
                ShoulderColor = LogColor > ShoulderMatch ? ShoulderColor : StraightColor;
             
                half3 t = saturate((LogColor - ToeMatch) / (ShoulderMatch - ToeMatch));
                t = ShoulderMatch < ToeMatch ? 1 - t : t;
                t = (3 - 2 * t)*t*t;
                half3 linearCV = lerp(ToeColor, ShoulderColor, t);
             
                // Post desaturate
                linearCV = lerp(dot(float3(linearCV), AP1_RGB2Y), linearCV, 0.93);
             
                // Returning positive AP1 values
                //return max(0, linearCV);
             
                // Convert to display primary encoding
                // Rendering space RGB to XYZ
                float3 XYZ = mul(AP1_2_XYZ_MAT, linearCV);
             
                // Apply CAT from ACES white point to assumed observer adapted white point
                XYZ = mul(D60_2_D65_CAT, XYZ);
             
                // CIE XYZ to display primaries
                linearCV = mul(XYZ_2_REC709_MAT, XYZ);
             
                linearCV = saturate(linearCV); //Protection to make negative return out.
             
                return linearCV;
            }
            
            //Film ToneMapping
            half4 FilmTomemapFrag (Varyings input) : SV_Target
            {
                float4 color = GetSource(input.screenUV);
                float3 aces = ACEScg_to_ACES(unity_to_ACEScg(max(color.rgb, 0.0)));
                return half4(FilmicACESTonemap(aces),color.a);
            }

            
        ENDHLSL
        
        Pass
        {
            Name "NAES ToneMapping Pass"

            HLSLPROGRAM

            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment NAESTonemapFrag

            ENDHLSL
        }

        Pass
        {
            Name "GT ToneMapping Pass"

            HLSLPROGRAM

            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment GTTomemapFrag

            ENDHLSL
        }

        Pass
        {
            Name "Film ToneMapping Pass"

            HLSLPROGRAM

            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment FilmTomemapFrag

            ENDHLSL
        }

    }
}
