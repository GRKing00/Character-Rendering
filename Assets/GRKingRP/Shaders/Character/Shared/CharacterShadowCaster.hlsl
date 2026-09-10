#ifndef GRKING_CHARACTER_SHADOW_CASTER_INCLUDED
#define GRKING_CHARACTER_SHADOW_CASTER_INCLUDED
float3 _LightDirection;
float3 _LightPosition;

CharacterVaryings CharacterShadowVertex(CharacterAttributes input)
{
    CharacterVaryings output = CharacterVertex(input);
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirection = normalize(_LightPosition - output.positionWS);
#else
    float3 lightDirection = _LightDirection;
#endif
    output.positionCS = TransformWorldToHClip(
        ApplyShadowBias(output.positionWS, normalize(output.normalWS), lightDirection));
#if UNITY_REVERSED_Z
    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
    return output;
}
#endif
