// Body effects adapted from StarRailNPRShader.
// Copyright (C) 2023 Stalo <stalowork@163.com>
// SPDX-License-Identifier: GPL-3.0-or-later
#ifndef GRKING_CHARACTER_BODY_EFFECTS_INCLUDED
#define GRKING_CHARACTER_BODY_EFFECTS_INCLUDED

TEXTURE2D(_GRKingBodyDepthTexture);
SAMPLER(sampler_GRKingBodyDepthTexture);
float _GRKingScreenSpaceRimEnabled;

float CharacterBodyLinearEyeDepth(float depth)
{
    return IsPerspectiveProjection()
        ? LinearEyeDepth(depth, _ZBufferParams)
        : LinearDepthToEyeDepth(depth);
}

half3 ApplyCharacterStockings(half3 baseColor, float2 uv, float noV)
{
    if (_StockingsEnabled < 0.5)
        return baseColor;

    half4 stockingsMap = SAMPLE_TEXTURE2D(
        _StockingsMap,
        sampler_StockingsMap,
        TRANSFORM_TEX(uv, _StockingsMap));

    noV = saturate(noV);
    float power = max(0.04, _StockingsPower);
    float darkWidth = max(0.0001, _StockingsDarkWidth * power);
    float darkIntensity = saturate((noV - power) / (darkWidth - power));
    darkIntensity *= 1.0 - _StockingsLightIntensity;
    darkIntensity *= stockingsMap.r;

    half3 darkColor = lerp(1.0h, _StockingsDarkColor.rgb, darkIntensity);
    darkColor = lerp(1.0h, darkColor * baseColor, darkIntensity) * baseColor;

    float lightIntensity = lerp(0.5, 1.0,
        stockingsMap.b * _StockingsRoughness);
    lightIntensity *= stockingsMap.g * _StockingsLightIntensity;
    lightIntensity *= max(0.004, pow(noV, _StockingsLightWidth));

    half3 stockingsColor =
        lightIntensity * (darkColor + _StockingsColor.rgb) + darkColor;
    float stockingsMask = step(0.01, stockingsMap.r);
    return lerp(baseColor, stockingsColor, stockingsMask);
}

half3 GetCharacterRimLight(float4 positionCS, float3 normalWS, float3 viewWS,
    float noL, half4 lightMap, Light light, float modelScale)
{
    if (_GRKingScreenSpaceRimEnabled < 0.5 ||
        _RimEnabled < 0.5 || _RimIntensity <= 0.0)
        return 0.0h;

    modelScale = max(modelScale, 0.0001);
    float rimWidth = _RimWidth / 2000.0;
    rimWidth *= lightMap.r * _ScaledScreenParams.y;

    if (IsPerspectiveProjection())
        rimWidth *= unity_CameraProjection._m11 / 2.414;
    else
        rimWidth *= unity_CameraProjection._m11 / 1.5996;

    float depth = CharacterBodyLinearEyeDepth(positionCS.z);
    rimWidth *= 10.0 * rsqrt(max(depth / modelScale, 0.0001));

    float horizontalDirection = -sign(cross(viewWS, normalWS).y);
    // 归一化 UV 适配不同分辨率的身体深度纹理。
    float2 sampleUV = saturate(
        (positionCS.xy + float2(horizontalDirection * rimWidth, 0.0))
        / _ScaledScreenParams.xy);

    float offsetDepth = SAMPLE_TEXTURE2D(
        _GRKingBodyDepthTexture,
        sampler_GRKingBodyDepthTexture,
        sampleUV).r;
    offsetDepth = CharacterBodyLinearEyeDepth(offsetDepth);

    float depthEdge = smoothstep(
        0.12,
        0.18,
        (offsetDepth - depth) / modelScale);
    float fresnel = pow(
        max(1.0 - saturate(dot(normalWS, viewWS)), 0.01),
        max(_RimSoftness, 0.01));
    float rim = saturate(depthEdge * fresnel);

    float attenuation = saturate(
        noL * light.shadowAttenuation * light.distanceAttenuation);
    float lighting = lerp(saturate(_RimDark), 1.0, attenuation);
    return _RimColor.rgb * light.color * rim * lighting * max(0.0, _RimIntensity);
}

half3 GetCharacterEmission(half3 baseColor, half baseAlpha)
{
    if (_EmissionEnabled < 0.5 || _EmissionIntensity <= 0.0)
        return 0.0h;

    float mask = 1.0 - step(baseAlpha, _EmissionThreshold);
    return baseColor * _EmissionColor.rgb * mask * max(0.0, _EmissionIntensity);
}

#endif
