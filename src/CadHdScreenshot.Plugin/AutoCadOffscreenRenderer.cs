using System;
using System.Collections.Generic;
using System.Drawing;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.GraphicsSystem;
using CadHdScreenshot.Core;

namespace CadHdScreenshot.Plugin
{
	internal sealed class AutoCadOffscreenRenderer
	{
		public Bitmap Render(Document document, CaptureSelection selection, PixelSize targetSize, Color backgroundColor)
		{
			if (document == null)
			{
				throw new ArgumentNullException("document");
			}
			if (selection == null)
			{
				throw new ArgumentNullException("selection");
			}
			Manager graphicsManager = document.GraphicsManager;
			using (KernelDescriptor kernelDescriptor = new KernelDescriptor())
			{
				kernelDescriptor.addRequirement(KernelDescriptor.Drawing3D);
				GraphicsKernel kernel = Manager.AcquireGraphicsKernel(kernelDescriptor);
				try
				{
					return RenderWithKernel(graphicsManager, document, selection, targetSize, backgroundColor, kernel);
				}
				finally
				{
					Manager.ReleaseGraphicsKernel(kernel);
				}
			}
		}

		private static Bitmap RenderWithKernel(Manager manager, Document document, CaptureSelection selection, PixelSize targetSize, Color backgroundColor, GraphicsKernel kernel)
		{
			using (Device device = manager.CreateAutoCADOffScreenDevice(kernel))
			{
				using (Model model = manager.CreateAutoCADModel(kernel))
				{
					using (View view = manager.CreateView(device))
					{
						device.DeviceRenderType = RendererType.Default;
						device.BackgroundColor = backgroundColor;
						device.OnSize(new Size(targetSize.Width, targetSize.Height));
						device.Add(view);
						int viewportNumber = Convert.ToInt32(Autodesk.AutoCAD.ApplicationServices.Core.Application.GetSystemVariable("CVPORT"));
						manager.SetViewFromViewport(view, viewportNumber);
						view.ViewportExtents = new Extents2d(0.0, 0.0, 1.0, 1.0);
						using (Transaction transaction = document.Database.TransactionManager.StartOpenCloseTransaction())
						{
							foreach (Drawable item in OpenDrawables(transaction, document.Database, selection))
							{
								model.AddSceneGraphRoot(item);
								view.Add(item, model);
							}
							view.ZoomExtents(selection.Extents.MinPoint, selection.Extents.MaxPoint);
							device.Update();
							using (Bitmap original = device.GetSnapshot(new Rectangle(0, 0, targetSize.Width, targetSize.Height)))
							{
								return new Bitmap(original);
							}
						}
					}
				}
			}
		}

		private static IReadOnlyList<Drawable> OpenDrawables(Transaction transaction, Database database, CaptureSelection selection)
		{
			List<Drawable> list = new List<Drawable>();
			if (selection.RenderEntireModelSpace)
			{
				BlockTable blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
				BlockTableRecord item = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);
				list.Add(item);
				return list;
			}
			ObjectId[] objectIds = selection.ObjectIds;
			foreach (ObjectId id in objectIds)
			{
				Drawable drawable = transaction.GetObject(id, OpenMode.ForRead, openErased: false);
				if (drawable != null)
				{
					list.Add(drawable);
				}
			}
			return list;
		}
	}
}
