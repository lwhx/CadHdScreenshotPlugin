using System;
using System.Collections.Generic;
using System.Drawing;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.GraphicsSystem;
using CadHdScreenshot.Core;
using GsModel = Autodesk.AutoCAD.GraphicsSystem.Model;
using GsView = Autodesk.AutoCAD.GraphicsSystem.View;

namespace CadHdScreenshot.Plugin
{
    internal sealed class AutoCadOffscreenRenderer
    {
        public Bitmap Render(
            Document document,
            CaptureSelection selection,
            PixelSize targetSize,
            Color backgroundColor)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (selection == null)
            {
                throw new ArgumentNullException(nameof(selection));
            }

            Manager manager = document.GraphicsManager;
            using (KernelDescriptor descriptor = new KernelDescriptor())
            {
                // Off-screen devices require the 3D Drawing kernel, including for 2D drawings.
                descriptor.addRequirement(KernelDescriptor.Drawing3D);
                GraphicsKernel kernel = Manager.AcquireGraphicsKernel(descriptor);

                try
                {
                    return RenderWithKernel(manager, document, selection, targetSize, backgroundColor, kernel);
                }
                finally
                {
                    Manager.ReleaseGraphicsKernel(kernel);
                }
            }
        }

        private static Bitmap RenderWithKernel(
            Manager manager,
            Document document,
            CaptureSelection selection,
            PixelSize targetSize,
            Color backgroundColor,
            GraphicsKernel kernel)
        {
            using (Device device = manager.CreateAutoCADOffScreenDevice(kernel))
            using (GsModel model = manager.CreateAutoCADModel(kernel))
            using (GsView view = manager.CreateView(device))
            {
                device.DeviceRenderType = RendererType.Default;
                device.BackgroundColor = backgroundColor;
                device.OnSize(new Size(targetSize.Width, targetSize.Height));
                device.Add(view);
                int viewportNumber = Convert.ToInt32(
                    Autodesk.AutoCAD.ApplicationServices.Core.Application.GetSystemVariable("CVPORT"));
                manager.SetViewFromViewport(view, viewportNumber);
                // ViewportExtents uses normalized device coordinates, not output pixels.
                view.ViewportExtents = new Extents2d(0, 0, 1, 1);

                using (Transaction transaction = document.Database.TransactionManager.StartOpenCloseTransaction())
                {
                    IReadOnlyList<Drawable> drawables = OpenDrawables(transaction, document.Database, selection);
                    foreach (Drawable drawable in drawables)
                    {
                        model.AddSceneGraphRoot(drawable);
                        view.Add(drawable, model);
                    }

                    view.ZoomExtents(selection.Extents.MinPoint, selection.Extents.MaxPoint);
                    device.Update();

                    using (Bitmap snapshot = device.GetSnapshot(
                        new Rectangle(0, 0, targetSize.Width, targetSize.Height)))
                    {
                        return new Bitmap(snapshot);
                    }
                }
            }
        }

        private static IReadOnlyList<Drawable> OpenDrawables(
            Transaction transaction,
            Database database,
            CaptureSelection selection)
        {
            List<Drawable> drawables = new List<Drawable>();

            if (selection.RenderEntireModelSpace)
            {
                BlockTable blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(
                    blockTable[BlockTableRecord.ModelSpace],
                    OpenMode.ForRead);
                drawables.Add(modelSpace);
                return drawables;
            }

            foreach (ObjectId objectId in selection.ObjectIds)
            {
                Drawable drawable = transaction.GetObject(objectId, OpenMode.ForRead, false) as Drawable;
                if (drawable != null)
                {
                    drawables.Add(drawable);
                }
            }

            return drawables;
        }
    }
}
