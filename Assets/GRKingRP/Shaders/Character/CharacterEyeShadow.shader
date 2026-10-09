Shader "GRKingRP/Character/EyeShadow"
{
    Properties
    {
        [Main(EyeShadow, _, on, off)] _EyeShadow("Eye Shadow", Float) = 0
        [Sub(EyeShadow)] [MainColor] _EyeShadowColor("Color", Color) = (0.677,0.704,0.802,1)
        [Sub(EyeShadow)] _Intensity("Intensity", Range(0,1)) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }

        Pass
        {
            Name "Character Eye Shadow"
            Tags { "LightMode"="GRKingCharacterEyeShadow" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend DstColor Zero
            ColorMask RGB
            Stencil
            {
                Ref 2
                ReadMask 2
                WriteMask 0
                Comp Equal
                Pass Keep
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CharacterEyeShadowVertex
            #pragma fragment CharacterEyeShadowFragment
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _EyeShadowColor;
                float _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float fogFactor : TEXCOORD0;
            };

            Varyings CharacterEyeShadowVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 CharacterEyeShadowFragment(Varyings input) : SV_Target
            {
                half3 color = lerp(1.0h, _EyeShadowColor.rgb, _Intensity);
                // 雾中渐变到乘法的中性值 1，避免再次压暗已经混合雾的脸部。
                color = MixFogColor(color, half3(1.0h, 1.0h, 1.0h), input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
    CustomEditor "LWGUI.LWGUI"
}
