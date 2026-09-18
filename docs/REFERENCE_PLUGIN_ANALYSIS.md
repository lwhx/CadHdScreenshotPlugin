# DTU-Capture 6.0 静态分析摘要

分析对象：用户提供的 `DTU-Capture.dll` 与 `刀图CAD超清截图DTCQJT.VLX`。分析仅使用静态读取与反编译，没有执行第三方代码。

## 可确认的实现线索

- DLL 是 .NET 程序集，程序集与文件版本均为 6.0.0.0，未进行 Authenticode 签名。
- DLL 经过名称混淆和方法体保护，常规反编译只能恢复类型、签名、引用和部分包装方法。
- 可见的 AutoCAD Lisp 入口包括 `DTU-Capture`、`DTU-Capture-Independent`、`DTU-Capture-HXX`、`dtcqjts`、`dtcqjts2`、`dtjtrezoom` 与 `dtjtalert`。
- 截图相关引用包括 `Document.CapturePreviewImage`、`GetCurrentView`、`SetCurrentView`、`ViewTableRecord`、`Bitmap`、`Clipboard.SetDataObject`、`Clipboard.SetImage` 和 PNG 编码。
- 一个可恢复的委托签名为 `Bitmap (Document, uint, uint)`，与 `Document.CapturePreviewImage(width, height)` 完全匹配。
- VLX 头为 `VRTLIB-1`，标记为 protected、active-x、separate-namespace，包含 `syz-vlx-path` 与 `DTU-Capture` 两个 FAS 模块。

## 对本项目的影响

参考插件采用“调整当前视图 -> AutoCAD 原生视口捕获 -> 恢复视图”的核心路径。该路径与独立 GraphicsSystem 离屏设备对 RasterImage 的缓存和采样方式不同。

参考截图为 1522×1564，与测试机器当前视口约 761×782 的 2× 结果吻合。因此其尺寸策略更接近“截图范围充满视口后按倍率捕获”，不是固定 3000/5000/8000 像素长边。

本项目从 1.0.7 起在“原色 + 框选区域”中采用同类的 AutoCAD 原生 `CapturePreviewImage` 路径，以提高插入 JPG/PNG 底图与视口显示的一致性；1.0.8 恢复基于视口的 1×/2×/3× 尺寸策略。对象隔离截图及需要受控背景的模式仍使用现有离屏渲染器。
