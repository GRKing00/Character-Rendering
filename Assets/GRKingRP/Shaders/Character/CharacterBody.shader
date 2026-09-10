Shader "GRKingRP/Character/Body"
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
        [Sub(Lighting)] _LightMap("Light Map (R Specular G AO B Threshold A ID)", 2D) = "white" {}
        [Sub(Lighting)] _RampCool("Cool Ramp", 2D) = "white" {}
        [Sub(Lighting)] _RampWarm("Warm Ramp", 2D) = "white" {}
        [Sub(Lighting)] _RampWarmWeight("Warm Weight", Range(0,1)) = 0
        [SubToggle(Lighting)] _UseVertexAO("Use Vertex R AO", Float) = 1

        [Main(Outline, _, on, off)] _Outline("Outline", Float) = 0
        [SubToggle(Outline)] _OutlineEnabled("Enabled", Float) = 1
        [Sub(Outline)] _OutlineWidth("Width", Range(0,5)) = 1
        [Sub(Outline)] _OutlineDepthOffset("Depth Offset (Away From Camera)", Range(0,0.1)) = 0
        [Sub(Outline)] _ModelScale("Model Scale", Float) = 1
        [SubToggle(Outline)] _OutlineUseTangent("Use Smoothed Normal In Tangent", Float) = 1
        [SubToggle(Outline)] _OutlineUseVertexAlpha("Use Vertex Alpha Width", Float) = 1

        [Main(Material0, _, off, off)] _Material0("Material ID 0", Float) = 0
        [Sub(Material0)] [HDR] _SpecularColor0("Specular Color", Color) = (1,1,1,1)
        [Sub(Material0)] _Shininess0("Shininess", Range(1,256)) = 32
        [Sub(Material0)] _SpecularIntensity0("Specular Intensity", Range(0,10)) = 1
        [Sub(Material0)] _SpecularSoftness0("Specular Softness", Range(0.001,1)) = 0.05
        [Sub(Material0)] _OutlineColor0("Outline Color", Color) = (0.1,0.05,0.05,1)

        [Main(Material1, _, off, off)] _Material1("Material ID 1", Float) = 0
        [Sub(Material1)] [HDR] _SpecularColor1("Specular Color", Color) = (1,1,1,1)
        [Sub(Material1)] _Shininess1("Shininess", Range(1,256)) = 32
        [Sub(Material1)] _SpecularIntensity1("Specular Intensity", Range(0,10)) = 1
        [Sub(Material1)] _SpecularSoftness1("Specular Softness", Range(0.001,1)) = 0.05
        [Sub(Material1)] _OutlineColor1("Outline Color", Color) = (0.1,0.05,0.05,1)

        [Main(Material2, _, off, off)] _Material2("Material ID 2", Float) = 0
        [Sub(Material2)] [HDR] _SpecularColor2("Specular Color", Color) = (1,1,1,1)
        [Sub(Material2)] _Shininess2("Shininess", Range(1,256)) = 32
        [Sub(Material2)] _SpecularIntensity2("Specular Intensity", Range(0,10)) = 1
        [Sub(Material2)] _SpecularSoftness2("Specular Softness", Range(0.001,1)) = 0.05
        [Sub(Material2)] _OutlineColor2("Outline Color", Color) = (0.1,0.05,0.05,1)

        [Main(Material3, _, off, off)] _Material3("Material ID 3", Float) = 0
        [Sub(Material3)] [HDR] _SpecularColor3("Specular Color", Color) = (1,1,1,1)
        [Sub(Material3)] _Shininess3("Shininess", Range(1,256)) = 32
        [Sub(Material3)] _SpecularIntensity3("Specular Intensity", Range(0,10)) = 1
        [Sub(Material3)] _SpecularSoftness3("Specular Softness", Range(0.001,1)) = 0.05
        [Sub(Material3)] _OutlineColor3("Outline Color", Color) = (0.1,0.05,0.05,1)

        [Main(Material4, _, off, off)] _Material4("Material ID 4", Float) = 0
        [Sub(Material4)] [HDR] _SpecularColor4("Specular Color", Color) = (1,1,1,1)
        [Sub(Material4)] _Shininess4("Shininess", Range(1,256)) = 32
        [Sub(Material4)] _SpecularIntensity4("Specular Intensity", Range(0,10)) = 1
        [Sub(Material4)] _SpecularSoftness4("Specular Softness", Range(0.001,1)) = 0.05
        [Sub(Material4)] _OutlineColor4("Outline Color", Color) = (0.1,0.05,0.05,1)

        [Main(Material5, _, off, off)] _Material5("Material ID 5", Float) = 0
        [Sub(Material5)] [HDR] _SpecularColor5("Specular Color", Color) = (1,1,1,1)
        [Sub(Material5)] _Shininess5("Shininess", Range(1,256)) = 32
        [Sub(Material5)] _SpecularIntensity5("Specular Intensity", Range(0,10)) = 1
        [Sub(Material5)] _SpecularSoftness5("Specular Softness", Range(0.001,1)) = 0.05
        [Sub(Material5)] _OutlineColor5("Outline Color", Color) = (0.1,0.05,0.05,1)

        [Main(Material6, _, off, off)] _Material6("Material ID 6", Float) = 0
        [Sub(Material6)] [HDR] _SpecularColor6("Specular Color", Color) = (1,1,1,1)
        [Sub(Material6)] _Shininess6("Shininess", Range(1,256)) = 32
        [Sub(Material6)] _SpecularIntensity6("Specular Intensity", Range(0,10)) = 1
        [Sub(Material6)] _SpecularSoftness6("Specular Softness", Range(0.001,1)) = 0.05
        [Sub(Material6)] _OutlineColor6("Outline Color", Color) = (0.1,0.05,0.05,1)

        [Main(Material7, _, off, off)] _Material7("Material ID 7", Float) = 0
        [Sub(Material7)] [HDR] _SpecularColor7("Specular Color", Color) = (1,1,1,1)
        [Sub(Material7)] _Shininess7("Shininess", Range(1,256)) = 32
        [Sub(Material7)] _SpecularIntensity7("Specular Intensity", Range(0,10)) = 1
        [Sub(Material7)] _SpecularSoftness7("Specular Softness", Range(0.001,1)) = 0.05
        [Sub(Material7)] _OutlineColor7("Outline Color", Color) = (0.1,0.05,0.05,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "CharacterBody.hlsl"
        ENDHLSL

        Pass
        {
            Name "Character Body"
            Tags { "LightMode"="GRKingCharacterBody" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            Blend Off
            Stencil { Ref 1 WriteMask 1 Comp Always Pass Replace Fail Keep ZFail Keep }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CharacterVertex
            #pragma fragment CharacterBodyFragment
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "Character Outline"
            Tags { "LightMode"="GRKingCharacterOutline" }
            Cull Front
            ZWrite On
            ZTest LEqual
            Blend Off
            Stencil { Ref 1 WriteMask 1 Comp Always Pass Replace Fail Keep ZFail Keep }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CharacterOutlineVertex
            #pragma fragment CharacterOutlineFragment
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma multi_compile_fog
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
