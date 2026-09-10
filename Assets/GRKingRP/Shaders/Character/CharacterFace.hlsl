#ifndef GRKING_CHARACTER_FACE_INCLUDED
#define GRKING_CHARACTER_FACE_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_FaceMap); SAMPLER(sampler_FaceMap);

// 所有 Pass 保持同一份 UnityPerMaterial 布局。
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half4 _BackColor;
    float _Cutoff;
    float _BackfaceUV2;
    float _FaceMapUseUV2;
    half4 _FaceShadowColor;
    half4 _EyeShadowColor;
    float _EyeAlwaysLit;
    float4 _HeadForwardOS;
    float4 _HeadRightOS;
    float4 _HeadUpOS;
    float _OutlineEnabled;
    float _OutlineWidth;
    float _OutlineDepthOffset;
    float _OutlineUseTangent;
    float _OutlineUseVertexAlpha;
    float _ModelScale;
    half4 _OutlineColor;
CBUFFER_END

#include "Shared/CharacterCommon.hlsl"
#include "Shared/CharacterFaceLighting.hlsl"
#include "Shared/CharacterOutline.hlsl"
#include "Shared/CharacterDepthOnly.hlsl"
#include "Shared/CharacterDepthNormals.hlsl"
#include "Shared/CharacterShadowCaster.hlsl"

half4 CharacterFaceFragment(CharacterVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    half4 baseColor = CharacterBaseColor(input.uv.xy, true);
    CharacterClip(baseColor.a);

    float2 faceUV = _FaceMapUseUV2 > 0.5 ? input.uv.zw : input.uv.xy;
    half4 faceMap = SAMPLE_TEXTURE2D(_FaceMap, sampler_FaceMap, faceUV);

#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
#else
    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
#endif
    Light light = GetMainLight(shadowCoord);
    half3 color = GetCharacterFaceDiffuse(baseColor.rgb, faceMap, faceUV,
        light, GetCharacterHeadDirections());
    return half4(MixFog(color, input.fogFactor), baseColor.a);
}

half4 CharacterFaceOutlineFragment(CharacterVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    clip(_OutlineEnabled - 0.5);
    clip(_OutlineWidth - 0.00001);
    CharacterClip(CharacterBaseColor(input.uv.xy, true).a);
    return half4(MixFog(_OutlineColor.rgb, input.fogFactor), 1.0h);
}

half4 CharacterFaceEyeStencilFragment(CharacterVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    CharacterClip(CharacterBaseColor(input.uv.xy, true).a);
    float2 faceUV = _FaceMapUseUV2 > 0.5 ? input.uv.zw : input.uv.xy;
    clip(SAMPLE_TEXTURE2D(_FaceMap, sampler_FaceMap, faceUV).g - 0.5h);
    return 0.0h;
}

#endif
