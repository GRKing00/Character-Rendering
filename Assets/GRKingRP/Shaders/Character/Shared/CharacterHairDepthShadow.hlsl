// Front-hair shadow adapted from StarRailNPRShader.
// Copyright (C) 2023 Stalo <stalowork@163.com>
// SPDX-License-Identifier: GPL-3.0-or-later
#ifndef GRKING_CHARACTER_HAIR_DEPTH_SHADOW_INCLUDED
#define GRKING_CHARACTER_HAIR_DEPTH_SHADOW_INCLUDED

TEXTURE2D(_GRKingHairDepthTexture);
SAMPLER(sampler_GRKingHairDepthTexture);
float _GRKingHairShadowEnabled;

float CharacterLinearEyeDepth(float depth)
{
    return IsPerspectiveProjection()
        ? LinearEyeDepth(depth, _ZBufferParams)
        : LinearDepthToEyeDepth(depth);
}

float GetCharacterFrontHairShadow(float4 positionCS, float3 lightDirectionWS,
    float shadowDistance, float modelScale)
{
    float2 width = shadowDistance * 0.04;

    if (IsPerspectiveProjection())
        width *= unity_CameraProjection._m11 / 2.414;
    else
        width *= unity_CameraProjection._m11 / 1.5996;

    float faceDepth = CharacterLinearEyeDepth(positionCS.z);
    width *= max(modelScale, 0.0001) / max(faceDepth, 0.0001);
    width.x *= log10(max(_ScaledScreenParams.y, 1.0)) / 3.33;

    float2 lightDirectionVS = TransformWorldToViewDir(lightDirectionWS, true).xy;
    float2 screenUV = positionCS.xy / _ScaledScreenParams.xy;
    float2 offsetUV = saturate(screenUV + lightDirectionVS * width);
    float hairDepth = SAMPLE_TEXTURE2D(
        _GRKingHairDepthTexture,
        sampler_GRKingHairDepthTexture,
        offsetUV).r;
    hairDepth = CharacterLinearEyeDepth(hairDepth);

    return step(faceDepth, hairDepth);
}

#endif
