#ifndef GRKING_CHARACTER_MATERIAL_ID_INCLUDED
#define GRKING_CHARACTER_MATERIAL_ID_INCLUDED

// 与参考角色贴图一致：LightMap A 编码八个身体材质区域。
int GetCharacterMaterialID(half alpha)
{
    return clamp((int)floor(alpha * 8.0), 0, 7);
}

struct CharacterMaterialData
{
    half3 specularColor;
    float shininess;
    float intensity;
    float softness;
    half3 outlineColor;
};

CharacterMaterialData GetCharacterMaterial(int id)
{
    CharacterMaterialData data;
    if (id == 0)
    {
        data.specularColor = _SpecularColor0.rgb;
        data.shininess = _Shininess0;
        data.intensity = _SpecularIntensity0;
        data.softness = _SpecularSoftness0;
        data.outlineColor = _OutlineColor0.rgb;
    }
    else if (id == 1)
    {
        data.specularColor = _SpecularColor1.rgb;
        data.shininess = _Shininess1;
        data.intensity = _SpecularIntensity1;
        data.softness = _SpecularSoftness1;
        data.outlineColor = _OutlineColor1.rgb;
    }
    else if (id == 2)
    {
        data.specularColor = _SpecularColor2.rgb;
        data.shininess = _Shininess2;
        data.intensity = _SpecularIntensity2;
        data.softness = _SpecularSoftness2;
        data.outlineColor = _OutlineColor2.rgb;
    }
    else if (id == 3)
    {
        data.specularColor = _SpecularColor3.rgb;
        data.shininess = _Shininess3;
        data.intensity = _SpecularIntensity3;
        data.softness = _SpecularSoftness3;
        data.outlineColor = _OutlineColor3.rgb;
    }
    else if (id == 4)
    {
        data.specularColor = _SpecularColor4.rgb;
        data.shininess = _Shininess4;
        data.intensity = _SpecularIntensity4;
        data.softness = _SpecularSoftness4;
        data.outlineColor = _OutlineColor4.rgb;
    }
    else if (id == 5)
    {
        data.specularColor = _SpecularColor5.rgb;
        data.shininess = _Shininess5;
        data.intensity = _SpecularIntensity5;
        data.softness = _SpecularSoftness5;
        data.outlineColor = _OutlineColor5.rgb;
    }
    else if (id == 6)
    {
        data.specularColor = _SpecularColor6.rgb;
        data.shininess = _Shininess6;
        data.intensity = _SpecularIntensity6;
        data.softness = _SpecularSoftness6;
        data.outlineColor = _OutlineColor6.rgb;
    }
    else if (id == 7)
    {
        data.specularColor = _SpecularColor7.rgb;
        data.shininess = _Shininess7;
        data.intensity = _SpecularIntensity7;
        data.softness = _SpecularSoftness7;
        data.outlineColor = _OutlineColor7.rgb;
    }
    else
    {
        data.specularColor = _SpecularColor0.rgb;
        data.shininess = _Shininess0;
        data.intensity = _SpecularIntensity0;
        data.softness = _SpecularSoftness0;
        data.outlineColor = _OutlineColor0.rgb;
    }
    return data;
}
#endif
