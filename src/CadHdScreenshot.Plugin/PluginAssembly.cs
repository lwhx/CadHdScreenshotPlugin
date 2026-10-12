using Autodesk.AutoCAD.Runtime;
using CadHdScreenshot.Plugin;
[assembly: ExtensionApplication(typeof(PluginEntryPoint))]
[assembly: CommandClass(typeof(CadHdScreenshot.Plugin.InputMethod.ImeCommands))]
[assembly: CommandClass(typeof(BlockMirrorCommands))]
