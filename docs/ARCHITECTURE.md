# 架构说明

## 1. 总体数据流

```text
AutoCAD 2024
   |
   +-- Ribbon: 高清截图
   |
   v
WPF 非模态浮动窗口
   |
   +-- 框选区域
   +-- 选择对象
   |
   v
1x 屏幕像素基准计算
   |
   v
目标分辨率计算（2x / 3x / 其他）
   |
   v
AutoCAD GraphicsSystem 离屏渲染
   |
   v
原始高分辨率位图
   |
   +-- 颜色模式
   +-- 白色转透明
   +-- 白边
   +-- PNG/JPG 编码
   |
   +--> 自动保存
   +--> 另存为
   +--> Windows 剪贴板
```

## 2. 关键设计决定

### 2.1 不使用普通屏幕截图作为高清核心

Windows PrintScreen、BitBlt 或对当前视口位图做插值放大，都不能满足项目要求。

高清模式必须让 AutoCAD 在目标输出尺寸重新绘制模型空间内容。

### 2.2 1× 跟随当前视口

1× 采用当前框选区域在 AutoCAD 客户区中实际占用的像素尺寸。这使插件的 2×/3× 使用手感接近现有工具。

### 2.3 2×/3× 是首要质量档位

高倍能力属于次要功能。任何针对 5×、8×、10× 的设计都不能增加 2×/3× 的失败率或显著拖慢日常截图。

### 2.4 RasterImage 是一级支持对象

客户提供 JPG/PNG 户型图并导入 AutoCAD 是核心业务场景，不是边缘兼容项。

离屏渲染必须同时包含：

- RasterImage 户型底图
- CAD 沙发块
- Polyline/Arc/Spline/Hatch
- Text/MText
- Dimension/Leader

## 3. 模块边界

### `CadHdScreenshot.Core`

不引用 Autodesk 程序集。

负责：

- `PixelSize`
- `PixelScaleCalculator`
- `PixelRiskEvaluator`
- 设置模型与持久化
- 图片后处理
- 文件命名和安全写入

### `CadHdScreenshot.Plugin`

引用 AutoCAD 2024 API。

负责：

- 插件生命周期
- Ribbon
- 浮动窗口
- CAD 选择交互
- 世界坐标/屏幕坐标映射
- 实体 Extents
- ViewState 恢复
- GraphicsSystem 离屏渲染
- ACI 颜色策略
- 剪贴板和对话框
- 截图工作流编排

## 4. 主要组件

```text
PluginEntryPoint
  |
  +-- RibbonInstaller
  |      +-- ShowWindowCommand
  |
  +-- ScreenshotWindow
         +-- ScreenshotViewModel
                |
                +-- CaptureCoordinator
                       |
                       +-- WindowSelector
                       +-- EntitySelector
                       +-- ScreenPixelMapper
                       +-- PixelScaleCalculator
                       +-- PixelRiskEvaluator
                       +-- AutoCadOffscreenRenderer
                       +-- ImageProcessor/Encoder
                       +-- FileOutput
                       +-- ClipboardOutput
```

## 5. 状态恢复

`ViewStateGuard` 必须采用 `IDisposable`/`try-finally` 思路。

无论发生：

- 正常完成
- 用户按 Esc
- AutoCAD API 抛异常
- 图片编码失败
- 剪贴板失败

都不能让用户原来的 DWG 内容或视图永久变化。

## 6. 像素安全

硬限制：

```text
width * height <= 500000000
```

建议风险区：

| 总像素 | 等级 |
|---:|---|
| <= 5000万 | 正常 |
| 5000万 - 1.5亿 | 高清 |
| 1.5亿 - 3亿 | 高风险 |
| 3亿 - 5亿 | 极限 |
| > 5亿 | 禁止 |

## 7. 输出失败隔离

保存文件和复制剪贴板是两个独立结果。

例如：

```text
PNG 保存成功
剪贴板被其他程序占用
```

应该返回：图片生成成功，但剪贴板复制失败；不能把整个截图任务判为失败。

## 8. 当前非目标

本项目当前不实现：

- 3D
- Layout / Paper Space 专用流程
- 户型比例校准
- 用户命令行入口
- 授权/联网验证
- 多 AutoCAD 版本兼容
