using System;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.EditorInput;

namespace CadHdScreenshot.Plugin
{
    internal sealed class RasterDisplayQualityGuard : IDisposable
    {
        private readonly object _originalValue;
        private readonly bool _restore;

        private RasterDisplayQualityGuard(object originalValue, bool restore)
        {
            _originalValue = originalValue;
            _restore = restore;
        }

        public static RasterDisplayQualityGuard Enable(Editor editor)
        {
            object originalValue = null;
            bool changed = false;

            try
            {
                originalValue = Application.GetSystemVariable("IMAGEQUALITY");
                changed = Convert.ToInt32(originalValue) != 1;
                if (changed)
                {
                    Application.SetSystemVariable("IMAGEQUALITY", 1);
                }

                // Force RasterImage display caches to be regenerated at high quality
                // before the off-screen device reads the model-space drawables.
                editor.Regen();
                return new RasterDisplayQualityGuard(originalValue, changed);
            }
            catch
            {
                if (changed)
                {
                    TryRestore(originalValue);
                }

                // Raster quality is an enhancement; a driver that rejects the
                // variable must not prevent vector-only drawings from rendering.
                return new RasterDisplayQualityGuard(null, false);
            }
        }

        public void Dispose()
        {
            if (_restore)
            {
                TryRestore(_originalValue);
            }
        }

        private static void TryRestore(object value)
        {
            try
            {
                Application.SetSystemVariable("IMAGEQUALITY", value);
            }
            catch
            {
                // Do not hide a successfully rendered screenshot if restoration fails.
            }
        }
    }
}
