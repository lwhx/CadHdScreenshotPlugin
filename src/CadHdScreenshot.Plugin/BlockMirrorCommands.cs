using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Runtime;

namespace CadHdScreenshot.Plugin
{
	public sealed class BlockMirrorCommands
	{
		[CommandMethod("CADFIXMIRRORTEXT", CommandFlags.Modal | CommandFlags.UsePickSet)]
		public static void FixMirroredBlockText()
		{
			var document = Application.DocumentManager.MdiActiveDocument;
			if (document != null)
			{
				MirroredBlockTextService.FixSelected(document);
			}
		}
	}
}
