Shader "GRKingRP/Character/Hair"
{
    Properties
    {
        [Main(Surface, _, on, off)] _Surface("Surface", Float) = 0
        [Tex(Surface, _BaseColor)] _BaseMap("Base Map", 2D) = "white" {}
        [HideInInspector] _BaseColor("Base Color", Color) = (1,1,1,1)
        [Sub(Surface)] _BackColor("Back Color", Color) = (1,1,1,1)
        [SubEnum(Surface, UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
        [SubToggle(Surface)] _BackfaceUV2("Back Face Uses UV2", Float) = 0
        [SubToggle(Surface, _ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 0
        [Sub(Surface_ALPHATEST_ON)] _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5

        [Main(Lighting, _, on, off)] _Lighting("Lighting", Float) = 0
        [Sub(Lighting)] [NoScaleOffset] _LightMap("Light Map (R Specular, G AO, B Threshold)", 2D) = "white" {}
        [Sub(Lighting)] [NoScaleOffset] _RampCool("Cool Ramp", 2D) = "white" {}
        [Sub(Lighting)] [NoScaleOffset] _RampWarm("Warm Ramp", 2D) = "white" {}
        [Sub(Lighting)] _RampWarmWeight("Warm Weight", Range(0,1)) = 0
        [SubToggle(Lighting)] _UseVertexAO("Use Vertex R AO", Float) = 1

        [Main(Specular, _, on, off)] _Specular("Specular", Float) = 0
        [Sub(Specular)] [HDR] _SpecularColor("Color", Color) = (1,1,1,1)
        [Sub(Specular)] _SpecularShininess("Shininess", Range(1,256)) = 10
        [Sub(Specular)] _SpecularIntensity("Intensity", Range(0,10)) = 1
        [Sub(Specular)] _SpecularSoftness("Softness", Range(0.001,1)) = 0.02

        [Main(FrontHair, _, on, off)] _FrontHair("Front Hair", Float) = 0
        [SubToggle(FrontHair)] _FrontHairTransparent("Transparent Over Eyes", Float) = 1
        [Sub(FrontHair)] _HairBlendAlpha("Blend Alpha", Range(0,1)) = 0.6

        [Main(Outline, _, on, off)] _Outline("Outline", Float) = 0
        [SubToggle(Outline)] _OutlineEnabled("Enabled", Float) = 1
        [Sub(Outline)] _OutlineWidth("Width", Range(0,5)) = 1
        [Sub(Outline)] _OutlineDepthOffset("Depth Offset (Away From Camera)", Range(0,0.1)) = 0
        [Sub(Outline)] _ModelScale("Model Scale", Float) = 1
        [SubToggle(Outline)] _OutlineUseTangent("Use Smoothed Normal In Tangent", Float) = 1
        [SubToggle(Outline)] _OutlineUseVertexAlpha("Use Vertex Alpha Width", Float) = 1
        [Sub(Outline)] _OutlineColor("Outline Color", Color) = (0.1,0.05,0.05,1)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+20" }
        HLSLINCLUDE
        #include "CharacterHair.hlsl"
        ENDHLSL

        Pass
        {
            Name "Character Hair Opaque"
            Tags { "LightMode"="GRKingCharacterHairOpaque" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            Blend Off
            Stencil
            {
                Ref 3
                ReadMask 2
                WriteMask 1
                Comp NotEqual
                Pass Replace
                Fail Keep
            }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CharacterVertex
            #pragma fragment CharacterHairOpaqueFragment
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "Character Hair Transparent"
            Tags { "LightMode"="GRKingCharacterHairTransparent" }
            Cull Back
            ZWrite On
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil
            {
                Ref 3
                ReadMask 2
                Comp Equal
                Pass Keep
                Fail Keep
            }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CharacterVertex
            #pragma fragment CharacterHairTransparentFragment
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "Character Hair Outline"
            Tags { "LightMode"="GRKingCharacterOutline" }
            Cull Front
            ZWrite On
            ZTest LEqual
            Blend Off
            Stencil { Ref 1 WriteMask 1 Comp Always Pass Replace Fail Keep ZFail Keep }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CharacterOutlineVertex
            #pragma fragment CharacterHairOutlineFragment
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "Character Hair Depth"
            Tags { "LightMode"="GRKingCharacterHairDepth" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CharacterVertex
            #pragma fragment CharacterDepthFragment
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CharacterShadowVertex
            #pragma fragment CharacterDepthFragment
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CharacterVertex
            #pragma fragment CharacterDepthFragment
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CharacterVertex
            #pragma fragment CharacterDepthNormalsFragment
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }

    Fallback Off
    CustomEditor "LWGUI.LWGUI"
}
