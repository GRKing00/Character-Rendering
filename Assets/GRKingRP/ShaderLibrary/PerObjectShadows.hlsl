#ifndef GRKING_PER_OBJECT_SHADOWS_INCLUDED
#define GRKING_PER_OBJECT_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

#define GRKING_MAX_SHADOW_COUNT 16

TEXTURE2D_SHADOW(_GRKingSceneShadowAtlas);
TEXTURE2D_SHADOW(_GRKingSelfShadowAtlas);
int _GRKingSceneShadowCount;
int _GRKingSelfShadowCount;
float4x4 _GRKingSceneShadowMatrices[GRKING_MAX_SHADOW_COUNT];
float4x4 _GRKingSelfShadowMatrices[GRKING_MAX_SHADOW_COUNT];
// xy = UV 最小值，zw = UV 最大值；不是 x/y/width/height。
float4 _GRKingSceneShadowRects[GRKING_MAX_SHADOW_COUNT];
float4 _GRKingSelfShadowRects[GRKING_MAX_SHADOW_COUNT];
float _GRKingSelfShadowCasterIds[GRKING_MAX_SHADOW_COUNT];
float4 _GRKingSceneShadowAtlas_TexelSize;
float4 _GRKingSelfShadowAtlas_TexelSize;

half SampleGRKingShadowTile(TEXTURE2D_SHADOW_PARAM(atlas, atlasSampler),
    float4 shadowCoord, float4 uvRect, float2 texelSize)
{
    if (any(shadowCoord.xy < uvRect.xy) || any(shadowCoord.xy > uvRect.zw) ||
        shadowCoord.z <= 0.0 || shadowCoord.z >= 1.0)
        return 1.0h;

    half shadow;
#if defined(_SHADOWS_SOFT)
    // 每个比较采样都限制在本 Tile 的像素中心范围内，防止相邻角色串影。
    float2 offset = texelSize * 0.5;
    shadow = SAMPLE_TEXTURE2D_SHADOW(atlas, atlasSampler,
        float3(clamp(shadowCoord.xy + float2(-offset.x, -offset.y), uvRect.xy, uvRect.zw), shadowCoord.z));
    shadow += SAMPLE_TEXTURE2D_SHADOW(atlas, atlasSampler,
        float3(clamp(shadowCoord.xy + float2(offset.x, -offset.y), uvRect.xy, uvRect.zw), shadowCoord.z));
    shadow += SAMPLE_TEXTURE2D_SHADOW(atlas, atlasSampler,
        float3(clamp(shadowCoord.xy + float2(-offset.x, offset.y), uvRect.xy, uvRect.zw), shadowCoord.z));
    shadow += SAMPLE_TEXTURE2D_SHADOW(atlas, atlasSampler,
        float3(clamp(shadowCoord.xy + offset, uvRect.xy, uvRect.zw), shadowCoord.z));
    shadow *= 0.25h;
#else
    shadow = SAMPLE_TEXTURE2D_SHADOW(atlas, atlasSampler, shadowCoord.xyz);
#endif
    return lerp(1.0h, shadow, GetMainLightShadowParams().x);
}

half SampleGRKingSceneShadow(float3 positionWS)
{
    half shadow = 1.0h;
    for (int tile = 0; tile < _GRKingSceneShadowCount; tile++)
    {
        float4 coord = mul(_GRKingSceneShadowMatrices[tile], float4(positionWS, 1.0));
        shadow = min(shadow, SampleGRKingShadowTile(
            TEXTURE2D_SHADOW_ARGS(_GRKingSceneShadowAtlas, sampler_LinearClampCompare),
            coord, _GRKingSceneShadowRects[tile], _GRKingSceneShadowAtlas_TexelSize.xy));
        if (shadow <= 0.0h)
            break;
    }
    return shadow;
}

half SampleGRKingSelfShadow(float3 positionWS, float casterId)
{
    if (casterId <= 0.0)
        return 1.0h;
    // 只比较 ID，匹配后才进行纹理采样；Tile 排序改变不需要更新角色材质。
    for (int tile = 0; tile < _GRKingSelfShadowCount; tile++)
    {
        if (_GRKingSelfShadowCasterIds[tile] == casterId)
        {
            float4 coord = mul(_GRKingSelfShadowMatrices[tile], float4(positionWS, 1.0));
            return SampleGRKingShadowTile(
                TEXTURE2D_SHADOW_ARGS(_GRKingSelfShadowAtlas, sampler_LinearClampCompare),
                coord, _GRKingSelfShadowRects[tile], _GRKingSelfShadowAtlas_TexelSize.xy);
        }
    }
    return 1.0h;
}
#endif
