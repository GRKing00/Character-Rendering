#ifndef GRKING_CHARACTER_HAIR_INCLUDED
#define GRKING_CHARACTER_HAIR_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_LightMap); SAMPLER(sampler_LightMap);
TEXTURE2D(_RampCool); SAMPLER(sampler_RampCool);
TEXTURE2D(_RampWarm); SAMPLER(sampler_RampWarm);

// 所有 Pass 保持同一份 UnityPerMaterial 布局。
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half4 _BackColor;
    float _Cutoff;
    float _BackfaceUV2;
    float _UseVertexAO;
    float _RampWarmWeight;
    half4 _SpecularColor;
    float _SpecularShininess;
    float _SpecularIntensity;
    float _SpecularSoftness;
    float _FrontHairTransparent;
    float _HairBlendAlpha;
    float _OutlineEnabled;
    float _OutlineWidth;
    float _OutlineDepthOffset;
    float _OutlineUseTangent;
    float _OutlineUseVertexAlpha;
    float _ModelScale;
    half4 _OutlineColor;
CBUFFER_END

#include "Shared/CharacterCommon.hlsl"
#include "Shared/CharacterHairLighting.hlsl"
#include "Shared/CharacterOutline.hlsl"
#include "Shared/CharacterDepthOnly.hlsl"
#include "Shared/CharacterDepthNormals.hlsl"
#include "Shared/CharacterShadowCaster.hlsl"

half3 GetCharacterHairColor(CharacterVaryings input, bool frontFace,
    out half surfaceAlpha)
{
    float2 uv = CharacterUV(input.uv, frontFace);
    half4 baseColor = CharacterBaseColor(uv, frontFace);
    CharacterClip(baseColor.a);
    surfaceAlpha = baseColor.a;

    half4 lightMap = SAMPLE_TEXTURE2D(_LightMap, sampler_LightMap, uv);
    float3 normalWS = normalize(input.normalWS) * (frontFace ? 1.0 : -1.0);
    float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
#else
    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
#endif
    Light light = GetMainLight(shadowCoord);
    float noL = dot(normalWS, light.direction);
    half3 color = GetCharacterHairDiffuse(baseColor.rgb, lightMap, input.color.r,
        noL, light);
    color += GetCharacterHairSpecular(baseColor.rgb, lightMap,
        dot(normalWS, viewWS), noL, light);
    return color;
}

half4 CharacterHairOpaqueFragment(CharacterVaryings input,
    FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    bool frontFace = IS_FRONT_VFACE(face, true, false);
    half surfaceAlpha;
    half3 color = GetCharacterHairColor(input, frontFace, surfaceAlpha);
    return half4(MixFog(color, input.fogFactor), 1.0h);
}

half4 CharacterHairTransparentFragment(CharacterVaryings input,
    FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    bool frontFace = IS_FRONT_VFACE(face, true, false);
    half surfaceAlpha;
    half3 color = GetCharacterHairColor(input, frontFace, surfaceAlpha);
    half alpha = lerp(1.0h, saturate(_HairBlendAlpha),
        saturate(_FrontHairTransparent));
    return half4(MixFog(color, input.fogFactor), alpha);
}

half4 CharacterHairOutlineFragment(CharacterVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    clip(_OutlineEnabled - 0.5);
    clip(_OutlineWidth - 0.00001);
    CharacterClip(CharacterBaseColor(input.uv.xy, true).a);
    return half4(MixFog(_OutlineColor.rgb, input.fogFactor), 1.0h);
}

#endif
