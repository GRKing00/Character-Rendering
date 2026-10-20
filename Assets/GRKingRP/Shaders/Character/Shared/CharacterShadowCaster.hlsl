#ifndef GRKING_CHARACTER_SHADOW_CASTER_INCLUDED
#define GRKING_CHARACTER_SHADOW_CASTER_INCLUDED
float3 _LightDirection;

CharacterVaryings CharacterShadowVertex(CharacterAttributes input)
{
    CharacterVaryings output = CharacterVertex(input);
    float3 normalWS = normalize(output.normalWS);
#if defined(_GRKING_CASTING_SELF_SHADOW)
    // 自阴影使用每份材质的世界空间 Bias，而不是主光的 texel Bias。
    float normalBias = (1.0 - saturate(dot(_LightDirection, normalWS))) * _SelfShadowNormalBias;
    float3 positionWS = output.positionWS +
        _LightDirection * _SelfShadowDepthBias + normalWS * normalBias;
#else
    float3 positionWS = ApplyShadowBias(output.positionWS, normalWS, _LightDirection);
#endif
    output.positionCS = TransformWorldToHClip(positionWS);
#if UNITY_REVERSED_Z
    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
    return output;
}
#endif
