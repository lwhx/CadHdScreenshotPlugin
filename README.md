# CadHdScreenshotPlugin

AutoCAD 2024 x64 高清截图插件，用于家具户型方案截图。

核心场景：在 AutoCAD 模型空间中将客户户型图（DWG 或插入的 JPG/PNG RasterImage）、沙发 CAD 模型和尺寸标注一起以 2×/3× 真正高分辨率重新渲染并导出为 PNG/JPG，便于发送给客户确认摆放方案。

> 当前状态：设计已确认，项目骨架已建立，正在进行 AutoCAD 2024 实机构建与核心截图能力开发。

## 文档

- [开发文档](docs/DEVELOPMENT.md)：开发环境、构建、安装、调试、代码结构和开发流程
- [架构说明](docs/ARCHITECTURE.md)：模块边界、数据流、离屏渲染和状态恢复原则
- [实机测试清单](docs/TESTING.md)：AutoCAD 2024 下 1×/2×/3×、RasterImage、尺寸和异常恢复验收

## 开发分支

当前开发分支：

```text
feature/cad-hd-screenshot
```

获取代码：

```powershell
git clone https://github.com/lwhx/CadHdScreenshotPlugin.git
cd CadHdScreenshotPlugin
git checkout feature/cad-hd-screenshot
```

在 Windows + AutoCAD 2024 + Visual Studio 2022 环境中构建：

```powershell
.\eng\build.ps1
```
