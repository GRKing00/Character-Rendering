#ifndef GRKING_CHARACTER_DEPTH_ONLY_INCLUDED
#define GRKING_CHARACTER_DEPTH_ONLY_INCLUDED
half4 CharacterDepthFragment(CharacterVaryings input,
    FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    bool frontFace = IS_FRONT_VFACE(face, true, false);
    CharacterClip(CharacterBaseColor(CharacterUV(input.uv, frontFace), frontFace).a);
    return 0;
}
#endif
