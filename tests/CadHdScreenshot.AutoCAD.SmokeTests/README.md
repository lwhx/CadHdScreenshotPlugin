# AutoCAD 原生渲染回归

这个测试必须在完整 AutoCAD 2024 内执行，不属于 NUnit，也不能用 accoreconsole 替代图形设备测试。
测试编译链接实际 AutoCadOffscreenRenderer 源码，避免复制一份渲染算法。

1. 使用 dotnet build 构建本目录项目，必要时指定 -p:AutoCAD2024Dir="实际 AutoCAD 2024 目录"。
2. 在独立 AutoCAD 测试会话中打开本目录的 cad-hd-smoke.dxf，不打开业务图纸。
3. NETLOAD 加载测试 DLL（及同目录的 Core DLL），执行 CADHD_RENDER_SMOKE。
4. 默认结果写入 %LOCALAPPDATA%\CadHdScreenshot\RenderSmoke；也可在启动测试进程前设置 CADHD_SMOKE_OUTPUT 指向专用测试目录。
5. result.txt 必须包含 PASS，并生成 800×480、1600×960、2400×1440 三张非空 PNG。人工查看线条、圆与文字。需要时可丢弃测试图纸变动；测试不保存 DWG。

测试命令会检查活动图纸名必须以 cad-hd-smoke 开头；只调用渲染器，不新增或修改图元。
图像尺寸和非白像素检查只能证明设备渲染成功及内容非空，不代替选区边界、尺寸标注、RasterImage、透明度和窗口交互验收。

2026-09-12 的实测过程：

- Drawing2D：AcquireGraphicsKernel 抛出 InvalidOperationException，复现用户日志。
- 只改 Drawing3D：内核创建通过，但错误的像素式 ViewportExtents 导致 800×480 空白图。
- Drawing3D + ViewportExtents(0,0,1,1)：三个倍率均通过；非白像素分别为 2980、6026、9063。

接口依据：

- https://blog.autodesk.io/graphic-changes-in-autocad-2015/
- https://blog.autodesk.io/the-property-viewviewport-is-not-available-any-more-in-autocad-net-2013-api/
