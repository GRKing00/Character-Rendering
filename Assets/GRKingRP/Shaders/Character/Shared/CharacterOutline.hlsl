// View-space outline sizing adapted from StarRailNPRShader:
// Copyright (C) 2023 Stalo <stalowork@163.com>
// https://github.com/stalomeow/StarRailNPRShader
// SPDX-License-Identifier: GPL-3.0-or-later
#ifndef GRKING_CHARACTER_OUTLINE_INCLUDED
#define GRKING_CHARACTER_OUTLINE_INCLUDED

CharacterVaryings CharacterOutlineVertex(CharacterAttributes input)
{
    CharacterVaryings output = CharacterVertex(input);
    // Tangent.xyz 已存模型空间平滑法线，不能再执行 *2-1 解码。
    //选择描边外扩方向
    float3 normalOS = _OutlineUseTangent > 0.5 ? input.tangentOS.xyz : input.normalOS;
    if (dot(normalOS, normalOS) < 0.000001)
        normalOS = input.normalOS;
    //转换到观察空间
    float3 normalVS = TransformWorldToViewDir(TransformObjectToWorldNormal(normalOS));
    float3 positionVS = TransformWorldToView(output.positionWS);
    //计算基础宽度
    float modelScale = max(0.0001, _ModelScale);
    float vertexWidth = lerp(1.0, input.color.a, _OutlineUseVertexAlpha);
    float width = _OutlineWidth * modelScale * 0.0588 * vertexWidth;
    //补偿相机投影
    float projectionScale = (unity_OrthoParams.w > 0.5 ? 1.5996 : 2.414)
        / max(abs(UNITY_MATRIX_P._m11), 0.0001);
    //补偿相机距离
    float distanceScale = projectionScale * max(0.0, -positionVS.z) / modelScale;
    width *= clamp(distanceScale * 0.025, 0.04, 0.1);
    //沿法线外扩
    normalVS.z = -0.1;
    positionVS += normalize(normalVS) * width;
    // 正值将外扩壳向远离相机的方向推，减轻内部线条穿插。
    //描边深度偏移
    positionVS.z -= _OutlineDepthOffset * modelScale;
    output.positionCS = TransformWViewToHClip(positionVS);
    output.positionWS = TransformViewToWorld(positionVS);
    output.fogFactor = ComputeFogFactor(output.positionCS.z);
    return output;
}

#endif
