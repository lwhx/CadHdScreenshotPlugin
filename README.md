# CadHdScreenshotPlugin

AutoCAD 2024 x64 高清截图插件，用于家具户型方案截图。

核心场景：在 AutoCAD 模型空间中将客户户型图（DWG 或插入的 JPG/PNG RasterImage）、沙发 CAD 模型和尺寸标注一起以 2×/3× 真正高分辨率重新渲染并导出为 PNG/JPG，便于发送给客户确认摆放方案。

> 当前状态：Core、Ribbon、非模态窗口、选择交互、GraphicsSystem 离屏渲染、图片输出与安装脚本均已实现；等待 AutoCAD 2024 实机完成验收清单。

## 当前界面

- **高清截图**：横向选择 1× / 2× / 3×，点击原色、白底、黑白或黑白灰卡片后框选；完成后直接粘贴，Esc 取消。尺寸为当前视口参考值，实际输出随选区比例调整。
- **快捷命令设置**：展开后编辑命令并点击保存；无效输入会就地提示，未保存的编辑不会因截图或关闭窗口而写入配置。
- **标注样式**：选择样式与块内修改范围，再选择对象并应用；切换图纸后点击刷新。“所有同名块”会显示影响范围提醒。
- 操作期间禁用其他操作；底部状态信息可选中复制。缩小窗口时内容支持滚动，状态栏保持可见。

## 文档入口

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

构建完成后直接更新到当前用户的 AutoCAD 插件目录：

```powershell
.\eng\build.ps1 -Install
```

安装前需关闭 AutoCAD，脚本会将最新 bundle 写入 `%APPDATA%\Autodesk\ApplicationPlugins`。
