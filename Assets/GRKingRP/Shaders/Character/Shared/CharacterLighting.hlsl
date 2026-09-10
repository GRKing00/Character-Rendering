// Ramp / specular equations adapted from StarRailNPRShader:
// Copyright (C) 2023 Stalo <stalowork@163.com>
// https://github.com/stalomeow/StarRailNPRShader
// SPDX-License-Identifier: GPL-3.0-or-later
#ifndef GRKING_CHARACTER_LIGHTING_INCLUDED
#define GRKING_CHARACTER_LIGHTING_INCLUDED

float2 GetCharacterRampUV(float noL, float ao, float materialAlpha, float shadow)
{
    float halfLambert = noL * 0.5 + 0.5;
    float rampX = max(0.001, min(1.0, 4.0 * halfLambert * ao)) * 0.75 + 0.25;
    rampX = lerp(0.20, rampX, saturate(shadow));
    // 极暗 AO 固定为暗部，极亮 AO 固定为亮部。
    if (ao < 0.05) rampX = 0.0;
    if (ao > 0.95) rampX = 1.0;
    return float2(rampX, saturate(materialAlpha + 0.05));
}

half3 GetCharacterDiffuse(half3 baseColor, half4 lightMap, float vertexAO,
    float noL, Light light)
{
    float ao = lightMap.g * lerp(1.0, vertexAO, _UseVertexAO);
    float2 uv = GetCharacterRampUV(noL, ao, lightMap.a, light.shadowAttenuation);
    half3 cool = SAMPLE_TEXTURE2D(_RampCool, sampler_RampCool, uv).rgb;
    half3 warm = SAMPLE_TEXTURE2D(_RampWarm, sampler_RampWarm, uv).rgb;
    return baseColor * lerp(cool, warm, _RampWarmWeight) * light.color * light.distanceAttenuation;
}

half3 GetCharacterSpecular(half3 baseColor, half4 lightMap,
    CharacterMaterialData data, float noH, Light light)
{
    float highlight = pow(max(0.01, noH), max(1.0, data.shininess));
    highlight *= light.shadowAttenuation * saturate(light.distanceAttenuation);
    float threshold = 1.03 - lightMap.b;
    float softness = max(0.0001, data.softness);
    float mask = smoothstep(threshold - softness, threshold + softness, highlight);
    return baseColor * data.specularColor * light.color * mask * lightMap.r * data.intensity;
}
#endif
