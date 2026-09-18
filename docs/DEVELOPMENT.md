# 开发文档

本文档用于在 Windows + AutoCAD 2024 x64 环境中继续开发、构建、安装和调试 `CadHdScreenshotPlugin`。

## 1. 项目目标

插件面向家具销售场景：在 AutoCAD 模型空间中，将客户户型图（DWG 或插入的 JPG/PNG RasterImage）、沙发 CAD 模型、文字和尺寸标注一起导出为真正高分辨率 PNG/JPG。

核心要求：2×、3× 必须按目标像素重新渲染，不能把 1× 位图简单放大。

## 2. 开发环境

- Windows 10/11 x64
- AutoCAD 2024 x64 正版
- Visual Studio 2022
- .NET SDK（用于解析 SDK 风格项目；只有 .NET Runtime 或 MSBuild 不足以构建）
- .NET Framework 4.8 Developer Pack / Targeting Pack
- Git
- PowerShell 5.1+

AutoCAD 默认安装目录：

```text
C:\Program Files\Autodesk\AutoCAD 2024
```

项目会引用：

```text
AcCoreMgd.dll
AcDbMgd.dll
AcMgd.dll
AdWindows.dll
```

这些 Autodesk DLL 仅作为编译引用，不复制进插件包。

## 3. 获取代码

```powershell
git clone https://github.com/lwhx/CadHdScreenshotPlugin.git
cd CadHdScreenshotPlugin
git checkout feature/cad-hd-screenshot
```

开发阶段不要直接在 `main` 上工作。

## 4. 项目结构

```text
CadHdScreenshotPlugin.sln
Directory.Build.props
src/
  CadHdScreenshot.Core/      # 与 AutoCAD 解耦的纯 .NET 逻辑
  CadHdScreenshot.Plugin/    # AutoCAD API、Ribbon、WPF、截图渲染
packaging/
  CadHdScreenshot.bundle/    # AutoCAD ApplicationPlugins 包结构
eng/
  build.ps1                  # 构建入口
  install-user.ps1           # 后续加入：当前用户安装
  uninstall-user.ps1         # 后续加入：当前用户卸载
tests/
  CadHdScreenshot.Core.Tests/
docs/
```

### Core 项目职责

只放不依赖 Autodesk DLL 的代码：

- 1×/2×/3× 像素计算
- 像素风险评估
- 设置持久化
- PNG/JPG 后处理
- 白色转透明
- 文件命名和原子写入

这样可以脱离 AutoCAD 做自动化单元测试。

### Plugin 项目职责

只放 AutoCAD 相关代码：

- Ribbon 按钮
- 可拖动非模态 WPF 窗口
- 两点框选
- 对象选择和 Extents 计算
- 当前视口像素映射
- AutoCAD GraphicsSystem 离屏渲染
- RasterImage + 矢量对象联合输出
- 剪贴板
- AutoCAD 状态恢复

## 5. 构建

推荐打开 **Visual Studio 2022 Developer PowerShell** 后执行：

```powershell
.\eng\build.ps1
```

只开发或验证不依赖 AutoCAD 的 Core 时执行：

```powershell
.\eng\test-core.ps1
```

该脚本会自动定位 Visual Studio/Build Tools 中的 MSBuild 和 VSTest，不要求从 Developer PowerShell 启动。

脚本会：

1. 检查 AutoCAD 2024 DLL 是否存在。
2. Restore NuGet。
3. 运行 Core 单元测试。
4. 使用 MSBuild 编译解决方案。
5. 生成 AutoCAD `.bundle`。

如果 AutoCAD 安装在非默认目录：

```powershell
.\eng\build.ps1 -AutoCAD2024Dir "D:\Autodesk\AutoCAD 2024"
```

未传入 `-AutoCAD2024Dir` 时，脚本会查询 AutoCAD 2024 (`R24.3`) 注册表并检查常见安装目录。

预期产物：

```text
artifacts\CadHdScreenshot.bundle\
  PackageContents.xml
  Contents\Windows\
    CadHdScreenshot.Plugin.dll
    CadHdScreenshot.Core.dll
```

## 6. 安装到 AutoCAD

开发阶段可将生成的 bundle 复制到：

```text
%APPDATA%\Autodesk\ApplicationPlugins\CadHdScreenshot.bundle
```

也可以使用当前用户安装脚本：

```powershell
.\eng\install-user.ps1
```

卸载：

```powershell
.\eng\uninstall-user.ps1
```

然后重新启动 AutoCAD 2024。

插件最终通过 Ribbon 的“高清截图”按钮启动，不提供用户命令行入口。

## 7. Visual Studio 调试

在 `CadHdScreenshot.Plugin` 项目调试属性中，将外部程序设置为：

```text
C:\Program Files\Autodesk\AutoCAD 2024\acad.exe
```

建议：

1. 先执行 Release/Debug 构建并安装 bundle。
2. Visual Studio 附加/启动 `acad.exe`。
3. 在插件入口、截图协调器、渲染器上设置断点。
4. 使用专门的测试 DWG，避免拿生产图纸做异常测试。

## 8. 核心行为约束

### 像素倍率

1× 不是固定尺寸，而是当前 CAD 视口中选区实际占用的屏幕像素。

例如：

```text
1× = 1823 × 782
2× = 3646 × 1564
3× = 5469 × 2346
```

2×/3× 必须直接以目标尺寸重新渲染。

### 截图范围

支持两种模式：

- 两点矩形框选
- 选择对象后按整体几何 Extents 截图

### 输出格式

- PNG
- JPG

白色转透明时必须强制 PNG。

### 颜色模式

- 原色
- 黑白
- 白底
- 黑白灰
- 突出色 A
- 突出色 B

黑白灰模式默认保留 ACI：

```text
8, 9, 250, 251, 252, 253, 254
```

### 安全限制

```text
宽 × 高 <= 500,000,000
```

2×/3× 为最高优先级。高倍截图不能以牺牲普通截图稳定性为代价。

## 9. DWG 安全原则

截图功能不得永久修改：

- 图元位置
- 图层
- 颜色
- 尺寸
- RasterImage 变换
- 当前模型比例

如离屏渲染过程中需要临时修改视图或 GraphicsSystem 状态，必须在成功、取消和异常路径中全部恢复。

## 10. 测试策略

采用 TDD 开发 Core 逻辑。

自动化测试重点：

- 像素倍率
- 长边像素计算
- 5 亿像素限制
- 设置读写
- 图片透明处理
- 白边
- 文件命名
- 原子保存

AutoCAD 集成测试必须在 Windows + AutoCAD 2024 实机完成。

最重要的人工测试场景：

```text
客户 JPG/PNG 户型图
+
沙发 CAD Block
+
Dimension 尺寸
+
文字/引线
```

分别导出 1×、2×、3×，确认：

- RasterImage 不丢失、不偏移
- 沙发不变形
- DIM 数字和箭头清晰
- 3× 比 1× 拥有真实新增细节
- 截图后 CAD 视图和 DWG 内容不被改变

## 11. 开发流程

每个功能按下面顺序：

1. 写失败测试（适用于 Core）。
2. 确认测试因缺少功能而失败。
3. 写最小实现。
4. 测试通过。
5. 做必要重构。
6. 提交一个小而完整的 commit。

AutoCAD API 集成无法纯单测的部分，必须有对应的实机验证步骤和测试 DWG。

## 12. 当前开发分支

```text
feature/cad-hd-screenshot
```

当前阶段：Core、AutoCAD 选择交互、屏幕像素映射、GraphicsSystem 离屏渲染、Ribbon、非模态窗口、文件与剪贴板输出均已实现。下一步必须在安装 AutoCAD 2024 的实机上逐项执行 `docs/TESTING.md`，重点验证 RasterImage、Dimension、3× 真实细节和异常后的视图恢复。
