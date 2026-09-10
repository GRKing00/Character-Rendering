// Face SDF lighting adapted from StarRailNPRShader:
// Copyright (C) 2023 Stalo <stalowork@163.com>
// https://github.com/stalomeow/StarRailNPRShader
// SPDX-License-Identifier: GPL-3.0-or-later
#ifndef GRKING_CHARACTER_FACE_LIGHTING_INCLUDED
#define GRKING_CHARACTER_FACE_LIGHTING_INCLUDED

struct CharacterHeadDirections
{
    float3 forward;
    float3 right;
    float3 up;
};

CharacterHeadDirections GetCharacterHeadDirections()
{
    CharacterHeadDirections directions;
    directions.forward = normalize(TransformObjectToWorldDir(_HeadForwardOS.xyz));
    directions.right = normalize(TransformObjectToWorldDir(_HeadRightOS.xyz));
    directions.up = normalize(TransformObjectToWorldDir(_HeadUpOS.xyz));
    return directions;
}

half3 GetCharacterFaceDiffuse(half3 baseColor, half4 faceMap, float2 faceUV,
    Light light, CharacterHeadDirections headDirections)
{
    // 将光照方向投影到头部水平面，避免抬头、低头改变左右脸判断。
    float3 lightDirection = light.direction
        - dot(light.direction, headDirections.up) * headDirections.up;
    float lightLengthSq = dot(lightDirection, lightDirection);
    lightDirection = lightLengthSq > 0.000001
        ? lightDirection * rsqrt(lightLengthSq)
        : headDirections.forward;

    bool lightOnRight = dot(lightDirection, headDirections.right) > 0.0;
    float2 sdfUV = lightOnRight ? float2(1.0 - faceUV.x, faceUV.y) : faceUV;
    float threshold = SAMPLE_TEXTURE2D(_FaceMap, sampler_FaceMap, sdfUV).a;
    float forwardLight = dot(headDirections.forward, lightDirection) * 0.5 + 0.5;

    float faceLit = step(1.0 - threshold, forwardLight) * light.shadowAttenuation;
    half3 faceLighting = lerp(_FaceShadowColor.rgb, 1.0h, faceLit);

    float eyeLit = smoothstep(0.3, 0.5, forwardLight) * light.shadowAttenuation;
    half3 eyeLighting = lerp(_EyeShadowColor.rgb, 1.0h, eyeLit);
    half3 lighting = lerp(faceLighting, eyeLighting, faceMap.r);
    lighting *= light.color * light.distanceAttenuation;

    // FaceMap R 的中间区间表示眼睛；常亮只抵消光照，不改变 BaseMap。
    float eyeMask = step(0.1, faceMap.r) - step(0.8, faceMap.r);
    return baseColor * lerp(lighting, 1.0h, eyeMask * _EyeAlwaysLit);
}

#endif
