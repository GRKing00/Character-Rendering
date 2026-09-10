#ifndef GRKING_CHARACTER_BODY_INCLUDED
#define GRKING_CHARACTER_BODY_INCLUDED
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
    float _OutlineEnabled;
    float _OutlineWidth;
    float _OutlineDepthOffset;
    float _OutlineUseTangent;
    float _OutlineUseVertexAlpha;
    float _ModelScale;
    half4 _SpecularColor0;
    float _Shininess0;
    float _SpecularIntensity0;
    float _SpecularSoftness0;
    half4 _OutlineColor0;
    half4 _SpecularColor1;
    float _Shininess1;
    float _SpecularIntensity1;
    float _SpecularSoftness1;
    half4 _OutlineColor1;
    half4 _SpecularColor2;
    float _Shininess2;
    float _SpecularIntensity2;
    float _SpecularSoftness2;
    half4 _OutlineColor2;
    half4 _SpecularColor3;
    float _Shininess3;
    float _SpecularIntensity3;
    float _SpecularSoftness3;
    half4 _OutlineColor3;
    half4 _SpecularColor4;
    float _Shininess4;
    float _SpecularIntensity4;
    float _SpecularSoftness4;
    half4 _OutlineColor4;
    half4 _SpecularColor5;
    float _Shininess5;
    float _SpecularIntensity5;
    float _SpecularSoftness5;
    half4 _OutlineColor5;
    half4 _SpecularColor6;
    float _Shininess6;
    float _SpecularIntensity6;
    float _SpecularSoftness6;
    half4 _OutlineColor6;
    half4 _SpecularColor7;
    float _Shininess7;
    float _SpecularIntensity7;
    float _SpecularSoftness7;
    half4 _OutlineColor7;
CBUFFER_END

#include "Shared/CharacterCommon.hlsl"
#include "Shared/CharacterMaterialID.hlsl"
#include "Shared/CharacterLighting.hlsl"
#include "Shared/CharacterOutline.hlsl"
#include "Shared/CharacterDepthOnly.hlsl"
#include "Shared/CharacterDepthNormals.hlsl"
#include "Shared/CharacterShadowCaster.hlsl"

half4 CharacterBodyFragment(CharacterVaryings input,
    FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    bool frontFace = IS_FRONT_VFACE(face, true, false);
    float2 uv = CharacterUV(input.uv, frontFace);
    half4 baseColor = CharacterBaseColor(uv, frontFace);
    CharacterClip(baseColor.a);
    half4 lightMap = SAMPLE_TEXTURE2D(_LightMap, sampler_LightMap, uv);
    CharacterMaterialData material = GetCharacterMaterial(GetCharacterMaterialID(lightMap.a));
    float3 normalWS = normalize(input.normalWS) * (frontFace ? 1.0 : -1.0);
    float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
#else
    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
#endif
    Light light = GetMainLight(shadowCoord);
    float3 halfWS = SafeNormalize(light.direction + viewWS);
    half3 color = GetCharacterDiffuse(baseColor.rgb, lightMap, input.color.r,
        dot(normalWS, light.direction), light);
    color += GetCharacterSpecular(baseColor.rgb, lightMap, material,
        dot(normalWS, halfWS), light);
    return half4(MixFog(color, input.fogFactor), baseColor.a);
}
#endif
