Shader "Hidden/GRKingRP/ScreenSpaceShadows"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Scene Screen Shadows"
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Fragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "../ShaderLibrary/PerObjectShadows.hlsl"

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float depth = SampleSceneDepth(input.texcoord);
#if UNITY_REVERSED_Z
                if (depth <= 0.000001)
                    return 1.0h;
#else
                if (depth >= 0.999999)
                    return 1.0h;
                depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, depth);
#endif
                float3 positionWS = ComputeWorldSpacePosition(input.texcoord, depth, UNITY_MATRIX_I_VP);
                half cascadeShadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(positionWS));
                half shadow = min(cascadeShadow, SampleGRKingSceneShadow(positionWS));
                return half4(shadow, shadow, shadow, 1.0h);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
