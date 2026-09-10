#ifndef GRKING_CHARACTER_DEPTH_NORMALS_INCLUDED
#define GRKING_CHARACTER_DEPTH_NORMALS_INCLUDED
half4 CharacterDepthNormalsFragment(CharacterVaryings input,
    FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    //IS_FRONT_VFACE(face, 正面时返回的值, 背面时返回的值)
    bool frontFace = IS_FRONT_VFACE(face, true, false);
    CharacterClip(CharacterBaseColor(CharacterUV(input.uv, frontFace), frontFace).a);
    float3 normalWS = normalize(input.normalWS) * (frontFace ? 1.0 : -1.0);
#if defined(_GBUFFER_NORMALS_OCT)
    // 单位法线只有两个独立自由度，先通过八面体编码转换为二维坐标。
    // 将两个坐标分别量化为 12 bit，再打包进 R8G8B8，
    // 从而在同样 24 bit 存储空间下获得比直接存储 RGB8 法线更高的方向精度。
    float2 oct = PackNormalOctQuadEncode(normalWS) * 0.5 + 0.5;
    return half4(PackFloat2To888(saturate(oct)), 0);
#else
    return half4(normalWS, 0);
#endif
}
#endif
