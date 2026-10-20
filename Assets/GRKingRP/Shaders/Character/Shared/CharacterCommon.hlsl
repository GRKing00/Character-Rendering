#ifndef GRKING_CHARACTER_COMMON_INCLUDED
#define GRKING_CHARACTER_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "../../../ShaderLibrary/PerObjectShadows.hlsl"

int _GRKingMainLightShadowCascadeCount;

Light GetCharacterMainLight(float3 positionWS)
{
    Light light = GetMainLight();
#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    // 角色不读取 Scene Atlas 合成的屏幕阴影，直接接收场景级联阴影。
    // 屏幕阴影开启后 URP 会关闭 CASCADE 关键字，所以由 CPU 提供实际级联数量。
    int cascadeIndex = _GRKingMainLightShadowCascadeCount > 1
        ? (int)ComputeCascadeIndex(positionWS) : 0;
    float4 coord = float4(mul(_MainLightWorldToShadow[cascadeIndex], float4(positionWS, 1.0)).xyz, 0.0);
    ShadowSamplingData sampling = GetMainLightShadowSamplingData();
    sampling.softShadowQuality = SOFT_SHADOW_QUALITY_LOW;
    light.shadowAttenuation = SampleShadowmap(
        TEXTURE2D_ARGS(_MainLightShadowmapTexture, sampler_LinearClampCompare),
        coord, sampling, GetMainLightShadowParams(), false);
    light.shadowAttenuation = lerp(light.shadowAttenuation, 1.0h, GetMainLightShadowFade(positionWS));
#endif
    return light;
}

struct CharacterAttributes
{
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    float2 uv2 : TEXCOORD1;
    float4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct CharacterVaryings
{
    float4 positionCS : SV_POSITION;
    float4 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS : TEXCOORD2;
    float4 color : TEXCOORD3;
    float fogFactor : TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

CharacterVaryings CharacterVertex(CharacterAttributes input)
{
    CharacterVaryings output = (CharacterVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS);
    output.positionCS = position.positionCS;
    output.positionWS = position.positionWS;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.uv = float4(input.uv, input.uv2) * _BaseMap_ST.xyxy + _BaseMap_ST.zwzw;
    output.color = input.color;
    output.fogFactor = ComputeFogFactor(position.positionCS.z);
    return output;
}

float2 CharacterUV(float4 uv, bool frontFace)
{
    return !frontFace && _BackfaceUV2 > 0.5 ? uv.zw : uv.xy;
}

half4 CharacterBaseColor(float2 uv, bool frontFace)
{
    half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
    return color * (frontFace ? _BaseColor : _BackColor);
}

void CharacterClip(half alpha)
{
#if defined(_ALPHATEST_ON)
    clip(alpha - _Cutoff);
#endif
}
#endif
