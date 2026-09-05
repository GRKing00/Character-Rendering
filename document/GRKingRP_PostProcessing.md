# GRKingRP 后处理方案

适用版本：Unity `2022.3.62f2c1`、URP `14.0.12`。

当前方案包含 Bloom、Tone Mapping 和 FXAA，结构为：

```text
GRKingRendererFeature
  → GRKingPostProcessingPass
      → Bloom.shader
      → ToneMapping.shader
      → FXAA.shader
```

## 1. 使用方法

1. 在相机使用的 Renderer Data 中添加 `GRKingRendererFeature`。
2. 开启 Feature 的 `Enable Post Processing`。
3. 确认 Bloom、Tone Mapping 和 FXAA Shader 引用不为空。编辑器会尝试自动补齐引用，Renderer Data 会保存直接引用。
4. 关闭 Renderer Data 和相机的 URP 内置 `Post Processing`。
5. 在 Volume Profile 中添加：
   - `GRKingRP/Bloom`
   - `GRKingRP/Tone Mapping`
   - `GRKingRP/FXAA`
6. 勾选需要使用的 Override 参数。

Bloom 默认 `Intensity = 0`，Tone Mapping 默认 `Mode = None`，FXAA 默认 `Enabled = false`，所以刚添加组件时不会改变画面。

建议使用 Linear 颜色空间和 HDR 相机。Renderer Data 与 Camera 的 URP 内置 Post Processing、Camera Anti-aliasing 应保持关闭，避免重复后处理或重复抗锯齿。

## 2. 文件职责

| 文件 | 作用 |
| --- | --- |
| [GRKingRendererFeature.cs](<D:/TA/UnityProject/my/Character Rendering/Assets/GRKingRP/Runtime/RendererFeatures/GRKingRendererFeature.cs>) | 总入口；根据开关和 Volume 状态决定是否将 Pass 入队 |
| [GRKingPostProcessingPass.cs](<D:/TA/UnityProject/my/Character Rendering/Assets/GRKingRP/Runtime/Passes/GRKingPostProcessingPass.cs>) | 读取 Volume，管理 RTHandle，按顺序提交三种后处理 |
| [GRKingBloom.cs](<D:/TA/UnityProject/my/Character Rendering/Assets/GRKingRP/Runtime/PostProcessing/GRKingBloom.cs>) | Bloom Volume 参数 |
| [GRKingToneMapping.cs](<D:/TA/UnityProject/my/Character Rendering/Assets/GRKingRP/Runtime/PostProcessing/GRKingToneMapping.cs>) | Tone Mapping Volume 参数 |
| [GRKingFXAA.cs](<D:/TA/UnityProject/my/Character Rendering/Assets/GRKingRP/Runtime/PostProcessing/GRKingFXAA.cs>) | FXAA Volume 开关、阈值与质量参数 |
| [Bloom.shader](<D:/TA/UnityProject/my/Character Rendering/Assets/GRKingRP/Shaders/PostProcessing/Bloom.shader>) | 预过滤、模糊和 Bloom 合成 |
| [ToneMapping.shader](<D:/TA/UnityProject/my/Character Rendering/Assets/GRKingRP/Shaders/PostProcessing/ToneMapping.shader>) | NAES、GT、Film 三种算法 |
| [FXAA.shader](<D:/TA/UnityProject/my/Character Rendering/Assets/GRKingRP/Shaders/PostProcessing/FXAA.shader>) | FXAA 边缘检测、边缘搜索和子像素混合 |

## 3. 执行流程

Feature 只有在以下条件满足时才将 Pass 入队：

- `Enable Post Processing` 已开启。
- 不是 Preview 或 Reflection 相机。
- Bloom、Tone Mapping 或 FXAA 至少有一个处于激活状态。

Render Pass 执行在 `BeforeRenderingPostProcessing`：

```text
相机颜色
  → 复制原图，避免同一纹理同时读写
  → Bloom（如果开启）
  → Tone Mapping（如果开启）
  → FXAA（如果开启）
  → 写回相机颜色
```

`remainingEffects` 记录当前效果执行后还有多少效果。后面没有效果时直接写回相机；仍有效果时，在 `m_ColorCopy` 与 `m_EffectResult` 之间交替写入，避免读写同一张纹理。

三种效果全部开启时：

```text
Camera Color → m_ColorCopy
              → Bloom → m_EffectResult
              → Tone Mapping → m_ColorCopy
              → FXAA → Camera Color
```

只有一个效果时不需要 `m_EffectResult`，该效果读取原图副本并直接写回相机。

## 4. Bloom

### 4.1 Volume 参数

| 参数 | 默认值 | 说明 |
| --- | --- | --- |
| Mode | Additive | Additive 累加；Scatter 在高低分辨率结果间插值 |
| Intensity | 0 | 最终 Bloom 强度；0 表示关闭 |
| Characters Only | false | 仅提取 Stencil 最低位为 1 的角色区域生成 Bloom |
| Scatter | 0.7 | Scatter 模式的层间混合比例 |
| Threshold | 1 | 高亮提取阈值 |
| Threshold Knee | 0.5 | 阈值附近的软过渡宽度 |
| Fade Fireflies | false | 降低孤立极亮像素的权重，减少闪烁 |
| Max Iterations | 6 | 最大模糊层级数，范围 1～16 |
| Downscale Limit | 2 | 纹理宽或高低于此值时停止降采样 |
| Bicubic Upsampling | false | 合成时使用更高质量的双三次采样 |
| Ignore Render Scale | false | 使用相机原始像素尺寸建立 Bloom 金字塔 |

预过滤使用 `max(R, G, B)` 作为亮度，并通过 Threshold Knee 平滑阈值边缘。Fade Fireflies 开启后会进行五点加权采样，极亮像素的权重更低。

模糊过程先创建半分辨率预过滤纹理，之后每层宽高继续减半，并依次进行水平和垂直高斯模糊。完成降采样后，再从最小层逐级向上合成。

Additive 最终计算近似为：

```text
final = original + bloom × Intensity
```

Scatter 使用 `Scatter` 控制层间插值，使用 `Intensity` 控制最终效果。Scatter 的最终 Intensity 会限制在 `0～1`。

角色模式额外分配角色颜色纹理，绑定相机深度/模板附件，只清空颜色，再用 Character Copy Pass 提取标记区域。预过滤和模糊读取角色颜色，最终合成仍读取完整场景。Scatter 只扣除角色输入的阈值高亮，避免压暗背景。角色 Shader 需自行写入 Stencil 最低位 1；当前后处理不负责写入标记。

### 4.2 Shader Pass 索引

| 索引 | Pass |
| --- | --- |
| 0 | Prefilter |
| 1 | Prefilter Fireflies |
| 2 | Horizontal Blur |
| 3 | Vertical Blur |
| 4 | Add |
| 5 | Scatter |
| 6 | Scatter Final |
| 7 | Character Copy（Stencil 筛选） |

修改 Shader Pass 顺序时，必须同步修改 C# 中的 `BloomPass` 枚举。

## 5. Tone Mapping

| Mode | Shader Pass | 参数 |
| --- | --- | --- |
| None | 不执行 | 无 |
| NAES | 0 | A、B、C、D |
| GranTurismo | 1 | 最大亮度、对比度、线性段起点和长度、暗部幂次与偏移 |
| Film | 2 | Slope、Toe、Shoulder、Black Clip、White Clip |

NAES 使用曲线：

```text
y = x(Ax + B) / (x(Cx + D) + 0.14)
```

GT 由暗部、线性段和高亮肩部组合而成。Film 包含 ACES 色彩空间转换、Glow、红色修正和 Film 曲线。

Render Pass 会对 GT 和 Film 的部分极端参数做安全限制，避免 Shader 出现零分母。这些限制只作用于传给 Shader 的数值，不会修改 Volume Profile。

Volume Mode 包含 `None`，不能直接将枚举值当作 Shader Pass 索引；当前使用 switch 显式映射。

## 6. FXAA

FXAA 在 Tone Mapping 之后执行，适合处理已经映射到显示范围的颜色。Shader 使用绿色通道近似亮度，不占用 Alpha 存储 Luma，因此会保留原图 Alpha。

| 参数 | 默认值 | 说明 |
| --- | --- | --- |
| Enabled | false | 是否启用 FXAA |
| Quality | High | Low、Medium、High 分别使用较少到较多的边缘搜索步数 |
| Fixed Threshold | 0.0833 | 最低固定亮度差；越低，处理的边缘越多 |
| Relative Threshold | 0.166 | 相对于局部最高亮度的阈值 |
| Subpixel Blending | 0.75 | 子像素混合强度；越高越平滑，也越容易模糊 |

FXAA Shader Pass：

| 索引 | Pass |
| --- | --- |
| 0 | FXAA |

质量通过局部关键字 `FXAA_QUALITY_LOW`、`FXAA_QUALITY_MEDIUM` 切换；没有质量关键字时使用 High。阈值与混合参数通过 `_FXAAConfig` 上传。

## 7. 资源管理与注意事项

- 中间纹理使用 `RTHandle` 和 `RenderingUtils.ReAllocateIfNeeded()`，尺寸不变时可以复用。
- 所有中间纹理沿用 `cameraTargetDescriptor.graphicsFormat`，HDR 精度由 URP 统一配置。需要 HDR 高亮时，管线与相机都应允许 HDR；Alpha 是否可存储取决于相机颜色格式。
- 全分辨率效果使用 `m_ColorCopy` 和 `m_EffectResult` 进行 ping-pong；启用多个效果时仍只需要两张全分辨率中间纹理。
- 颜色复制统一使用 `Blitter.BlitCameraTexture()`，不再维护自定义 Copy Pass。效果绘制仍使用 `DrawProcedural + MaterialPropertyBlock`；FXAA 质量关键字设置在 FXAA Material 上。
- `m_BloomUp` 先作为水平模糊结果，向上合成时再次复用，减少额外纹理。
- `OnCameraCleanup()` 清除相机对应的 Volume 状态，但保留可复用纹理。
- Feature 重建或销毁时，`Dispose()` 会释放材质和全部自有 RTHandle。
- 关闭 `Enable Post Processing` 只停止 Pass 入队，不会立即释放已经缓存的纹理。

常见问题：

| 问题 | 检查项 |
| --- | --- |
| 完全没有效果 | Renderer Data 是否添加 Feature；Feature 开关以及 Volume 效果是否激活 |
| Volume 参数无效 | 是否使用 GRKingRP 组件；Override、Weight 和 Volume Layer Mask 是否正确 |
| Bloom 不明显 | Intensity、Threshold、HDR 输入以及是否存在高亮区域 |
| FXAA 没有效果 | FXAA 的 Enabled、Volume Override、阈值和 Feature 开关 |
| 画面被重复处理 | 是否同时启用了 URP 内置后处理或 Camera Anti-aliasing |
| 构建后 Shader 丢失 | Shader 引用是否保存到了构建实际使用的 Renderer Data |

当前未实现 XR 双目、Render Graph、相机栈最终相机筛选和硬件动态分辨率的完整验证。后续加入逐对象阴影时，应在同一个 Feature 中使用独立开关和独立 Pass，不要让阴影功能依赖 `Enable Post Processing`。

`Temp/GRKingPostProcessingPassCheck/compile.rsp` 只是 C# 编译检查使用的临时响应文件，不参与运行，也不需要提交版本控制。
