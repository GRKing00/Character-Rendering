// Hair ramp / specular equations adapted from StarRailNPRShader:
// Copyright (C) 2023 Stalo <stalowork@163.com>
// https://github.com/stalomeow/StarRailNPRShader
// SPDX-License-Identifier: GPL-3.0-or-later
#ifndef GRKING_CHARACTER_HAIR_LIGHTING_INCLUDED
#define GRKING_CHARACTER_HAIR_LIGHTING_INCLUDED

float2 GetCharacterHairRampUV(float noL, float ao, float shadow)
{
    float halfLambert = noL * 0.5 + 0.5;
    float rampX = max(0.001, min(1.0, 4.0 * halfLambert * ao)) * 0.75 + 0.25;
    rampX = lerp(0.20, rampX, saturate(shadow));
    if (ao < 0.05) rampX = 0.0;
    if (ao > 0.95) rampX = 1.0;
    // 头发只有一套材质参数，固定采样 Ramp 最下面一行。
    return float2(rampX, 0.05);
}

half3 GetCharacterHairDiffuse(half3 baseColor, half4 lightMap, float vertexAO,
    float noL, Light light)
{
    float ao = lightMap.g * lerp(1.0, vertexAO, _UseVertexAO);
    float2 rampUV = GetCharacterHairRampUV(noL, ao, light.shadowAttenuation);
    half3 cool = SAMPLE_TEXTURE2D(_RampCool, sampler_RampCool, rampUV).rgb;
    half3 warm = SAMPLE_TEXTURE2D(_RampWarm, sampler_RampWarm, rampUV).rgb;
    return baseColor * lerp(cool, warm, _RampWarmWeight)
        * light.color * light.distanceAttenuation;
}

half3 GetCharacterHairSpecular(half3 baseColor, half4 lightMap,
    float noV, float noL, Light light)
{
    // 使用 NoV 让高光随视角在头发上流动；背光时关闭高光。
    float highlight = pow(max(0.01, saturate(noV)), max(1.0, _SpecularShininess));
    highlight *= step(0.0, noL) * light.shadowAttenuation
        * saturate(light.distanceAttenuation);
    float threshold = 1.03 - lightMap.b;
    float softness = max(0.0001, _SpecularSoftness);
    float mask = smoothstep(threshold - softness, threshold + softness, highlight);
    return baseColor * _SpecularColor.rgb * light.color
        * mask * lightMap.r * _SpecularIntensity;
}

#endif
