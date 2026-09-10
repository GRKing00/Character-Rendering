Shader "GRKingRP/Character/Face"
{
    Properties
    {
        [Main(Surface, _, on, off)] _Surface("Surface", Float) = 0
        [Tex(Surface, _BaseColor)] _BaseMap("Base Map", 2D) = "white" {}
        [HideInInspector] _BaseColor("Base Color", Color) = (1,1,1,1)
        [HideInInspector] _BackColor("Back Color", Color) = (1,1,1,1)
        [HideInInspector] _BackfaceUV2("Back Face Uses UV2", Float) = 0
        [SubEnum(Surface, UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [SubToggle(Surface, _ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 0
        [Sub(Surface_ALPHATEST_ON)] _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5

        [Main(FaceLighting, _, on, off)] _FaceLighting("Face Lighting", Float) = 0
        [Sub(FaceLighting)] [NoScaleOffset] _FaceMap("Face Map (R Eye, A SDF)", 2D) = "white" {}
        [SubToggle(FaceLighting)] _FaceMapUseUV2("Face Map Uses UV2", Float) = 0
        [Sub(FaceLighting)] _FaceShadowColor("Face Shadow Color", Color) = (0.5,0.5,0.5,1)
        [Sub(FaceLighting)] _EyeShadowColor("Eye Shadow Color", Color) = (1,1,1,1)
        [Sub(FaceLighting)] _EyeAlwaysLit("Eye Always Lit", Range(0,1)) = 0.2

        [Main(HeadDirections, _, on, off)] _HeadDirections("Head Directions", Float) = 0
        [Sub(HeadDirections)] _HeadForwardOS("Forward (Object Space)", Vector) = (0,1,0,0)
        [Sub(HeadDirections)] _HeadRightOS("Right (Object Space)", Vector) = (0,0,-1,0)
        [Sub(HeadDirections)] _HeadUpOS("Up (Object Space)", Vector) = (-1,0,0,0)

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
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "CharacterFace.hlsl"
        ENDHLSL

        Pass
        {
            Name "Character Face"
            Tags { "LightMode"="GRKingCharacterBody" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            Blend Off
            Stencil { Ref 1 WriteMask 1 Comp Always Pass Replace Fail Keep ZFail Keep }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CharacterVertex
            #pragma fragment CharacterFaceFragment
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "Character Face Outline"
            Tags { "LightMode"="GRKingCharacterOutline" }
            Cull Front
            ZWrite On
            ZTest LEqual
            Blend Off
            Stencil { Ref 1 WriteMask 1 Comp Always Pass Replace Fail Keep ZFail Keep }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CharacterOutlineVertex
            #pragma fragment CharacterFaceOutlineFragment
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
