# GRKingRP 资产导入处理方案

当前方案位于 `Assets/GRKingRP/Editor/AssetProcessors`，只在 Unity Editor 中运行。它通过两个 ScriptableObject 资产保存规则，不需要每次新增规则都修改 C#。

## 1. 配置入口

| 配置 | 固定路径 | 当前根目录 |
| --- | --- | --- |
| 纹理设置 | `Assets/GRKingRP/Settings/TextureImportSettings.asset` | `Assets/Textures/` |
| 模型设置 | `Assets/GRKingRP/Settings/ModelImportSettings.asset` | `Assets/Models/` |

根目录匹配包含所有子目录。根目录外的同名资产不会使用这些规则。

规则按列表从上到下匹配，第一条命中后停止。因此更具体的规则应放在更宽泛的规则前面。

### Glob 规则

- `*`：匹配任意数量字符。
- `?`：匹配一个字符。
- `|`：在同一字段中分隔多条可选模式。
- `IgnoreCase`：是否忽略大小写。
- 匹配的是不带扩展名的文件名，不是完整资产路径，也不是正则表达式。

示例：

```text
*_Color*             匹配名称中包含 _Color 的纹理
Art_* | Avatar_*     匹配以 Art_ 或 Avatar_ 开头的模型
```

`[Delayed]` 只控制 Inspector 文本框在回车或失去焦点后提交修改，不改变匹配算法。

## 2. 当前规则

纹理规则均忽略大小写：

| NameGlob | Preset |
| --- | --- |
| `*_Color*` | ColorTexture |
| `*_Ramp*` | RampTexture |
| `*_LightMap*` | LightMap |
| `*_Face_ExpressionMap*` | FaceExpressionMap |
| `*_FaceMap*` | FaceMap |

模型当前使用一条规则：

```text
NameGlob: Art_* | Avatar_*
Preset: AvatarModel
AutoBakeSmoothedNormals: true
SmoothedNormalStorage: Tangent
AutoCreatePrefab: true
AutoCreateMaterials: true
MaterialTemplate: None
```

`VerboseLogging` 只控制平滑法线处理的详细日志。

## 3. 纹理导入

[TextureImportPostprocessor.cs](<D:/TA/UnityProject/my/Character Rendering/Assets/GRKingRP/Editor/AssetProcessors/TextureImportPostprocessor.cs>) 在 `OnPreprocessTexture()` 中执行：

```text
读取 TextureImportSettings
  → 检查 TextureImportRootFolder
  → 获取不带扩展名的纹理名
  → 按顺序匹配规则
  → 将第一条匹配规则的 Preset 应用到 TextureImporter
```

Preset 必须能应用到 TextureImporter。规则未指定 Preset 时会报警告；无效类型会报错。

Importer 会登记对 Settings 资产和匹配 Preset 的 Artifact 依赖。修改设置或 Preset 后，Unity 可以使相关纹理重新导入。Preset 正在导入、暂时加载不到时也会登记依赖，等它可用后再次尝试。

新增规则时，只需在 `TextureImportSettings.asset` 的 Rules 中增加一项、配置 Glob 和 Preset，然后重新导入目标纹理。

## 4. 模型导入

[ModelImportPostprocessor.cs](<D:/TA/UnityProject/my/Character Rendering/Assets/GRKingRP/Editor/AssetProcessors/ModelImportPostprocessor.cs>) 分为两个阶段。

### OnPreprocessModel

模型数据生成前，将匹配规则的 Preset 应用到 ModelImporter。应用前会保存现有 External Object Map，应用后再恢复，避免 Preset 清掉已经建立的外部材质映射。

模型同样登记对 Settings 和 Preset Artifact 的依赖。

### OnPostprocessModel

模型和 Mesh 已生成后，如果 `AutoBakeSmoothedNormals` 开启，则计算平滑法线并写入规则指定的顶点通道。

这种分工避免了在 OnPreprocessModel 中访问尚未生成的 Mesh。

## 5. 平滑法线

[SmoothedNormalBaker.cs](<D:/TA/UnityProject/my/Character Rendering/Assets/GRKingRP/Editor/AssetProcessors/SmoothedNormalBaker.cs>) 会收集模型中的 MeshFilter 和 SkinnedMeshRenderer Mesh，并去除重复引用。

算法流程：

1. 遍历所有三角形 SubMesh。
2. 计算三角形面法线和每个顶点的夹角。
3. 使用“面法线 × 顶点夹角”作为贡献。
4. 按完全相同的模型空间顶点位置累加贡献。
5. 归一化结果，得到模型空间平滑法线。

这里没有外部平滑法线 Package 的 32 个重合顶点限制。需要注意，位置使用 `Vector3` 精确值作为字典键；只是非常接近但数值不同的顶点不会被合并。

| 存储位置 | 实际数据 | Shader 读取 |
| --- | --- | --- |
| Tangent.xyz | 模型空间平滑法线 | 不需要 `* 2 - 1`；再变换到世界空间 |
| VertexColor.rgb | 切线空间平滑法线，编码到 0～1 | 先执行 `color * 2 - 1`，再通过 TBN 转到世界空间 |
| UV2 / UV3 / UV4 | 未编码的切线空间平滑法线 | 不做 `* 2 - 1`；通过 TBN 转到世界空间 |

VertexColor 会保留已有 Alpha，但覆盖 RGB；UV 模式会覆盖对应 UV 通道。Tangent 模式会覆盖原始 tangent.xyz，只保留 tangent.w，因此不适合还需要原始切线进行普通法线贴图计算的材质。

## 6. 自动材质和 Prefab 输出

[ModelImportOutputPostprocessor.cs](<D:/TA/UnityProject/my/Character Rendering/Assets/GRKingRP/Editor/AssetProcessors/ModelImportOutputPostprocessor.cs>) 在 `OnPostprocessAllAssets()` 中收集刚导入或移动的模型，再通过 `EditorApplication.delayCall` 推迟到 AssetDatabase 较稳定的时机处理输出。

### 路径映射

输入模型在 Model 根目录下的相对目录会映射到输出根目录。

例如：

```text
模型：Assets/Models/Character/Avatar/Art_Robin_00.fbx
Prefab：Assets/Prefabs/Character/Avatar/Art_Robin_00.prefab
材质：Assets/Materials/Character/Avatar/Art_Robin_00/<材质槽名>.mat
```

Prefab 根节点使用模型名，导入模型实例作为其子节点。

### 自动材质

自动材质要求 ModelImporter 的 `Material Creation Mode = None`。代码通过 Unity 2022.3 的非公开接口读取 FBX 源材质槽，并开启 None 模式下的材质重映射；升级 Unity 后需要重点回归测试 [ModelImporterCompatibility.cs](<D:/TA/UnityProject/my/Character Rendering/Assets/GRKingRP/Editor/AssetProcessors/ModelImporterCompatibility.cs>)。

- 目标 `.mat` 不存在时，优先复制 `MaterialTemplate` 创建。
- 未设置模板时使用 `URP/Lit`，找不到时回退到 Standard。
- 已存在同名材质时直接复用，不覆盖已有材质参数。
- 创建后通过 `SourceAssetIdentifier → Material` 建立 External Object Map。
- 默认材质不会根据纹理命名自动填写 Base Map。

首次创建映射或修改兼容设置后会保存 Importer 并强制重新导入一次模型。再次处理时映射已经一致，不会无限循环。

### 自动 Prefab

Prefab 只在目标文件不存在时创建，已有 Prefab 不会被覆盖，避免丢失手动修改。若想重新自动生成，需要先自行处理现有 Prefab，再重新导入模型。

## 7. Settings、Preset 与重新导入

[AssetProcessorGlobalSettings.cs](<D:/TA/UnityProject/my/Character Rendering/Assets/GRKingRP/Editor/AssetProcessors/AssetProcessorGlobalSettings.cs>) 在程序集加载后延迟检查两份 Settings 资产。它们路径固定、不会自动创建；缺失时 Console 会报错。

`DependsOnArtifact` 的意义是把本次导入结果与配置或 Preset 的导入产物关联起来。每次需要依赖时都要在当前导入过程中重新声明，不能假设旧依赖永久保留。

修改 AssetPostprocessor 逻辑时，可以增加 `GetVersion()` 返回值，帮助 Unity 识别处理器版本变化；当前纹理和模型处理器版本均为 `7`。

## 8. 常见问题

| 问题 | 检查项 |
| --- | --- |
| 资产没有使用 Preset | 是否位于正确根目录；文件名是否匹配；规则是否 Enabled；Preset 类型是否正确 |
| 新规则不生效 | 第一条命中规则是否提前截断；修改是否已提交；重新导入目标资产 |
| 修改 Preset 后设置没更新 | Preset 是否为规则的直接引用；检查导入日志和 Artifact 依赖；重新导入验证 |
| 没有生成材质 | AutoCreateMaterials 是否开启；Material Creation Mode 是否为 None；模型是否存在源材质槽 |
| 材质没有自动关联纹理 | 当前只创建默认材质和槽位映射，没有实现按纹理名赋值 |
| 没有生成 Prefab | AutoCreatePrefab 是否开启；目标 Prefab 是否已经存在 |
| 模型首次导入两次 | 新建材质映射后需要重新导入模型，这是预期行为 |
| 法线贴图效果异常 | 是否将平滑法线写入 Tangent，导致原 tangent.xyz 被覆盖 |

修改规则前建议确认影响范围。扩大根目录或使用过宽的 `*` 会让更多资产依赖同一设置，并可能触发批量重新导入。
