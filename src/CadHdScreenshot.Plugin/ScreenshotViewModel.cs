using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using CadHdScreenshot.Core;

namespace CadHdScreenshot.Plugin
{
    internal sealed class ScreenshotViewModel : INotifyPropertyChanged
    {
        private readonly ScreenshotWindow _window;
        private readonly FileSettingsStore _settingsStore;
        private readonly CaptureCoordinator _coordinator;
        private readonly DimensionStyleService _dimensionStyleService;
        private Choice<double> _selectedScale;
        private Choice<ImageOutputFormat> _selectedFormat;
        private Choice<ScreenshotColorMode> _selectedColorMode;
        private bool _whiteToTransparent;
        private int _borderPixels;
        private bool _copyToClipboard;
        private bool _autoSave;
        private string _outputDirectory;
        private string _status;
        private string _previewWidth;
        private string _previewHeight;
        private string _customCommand;
        private IReadOnlyList<string> _dimensionStyleOptions = new string[0];
        private string _selectedDimensionStyle;
        private bool _modifySelectedBlockOnly = true;
        private bool _isBusy;

        public ScreenshotViewModel(ScreenshotWindow window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _settingsStore = FileSettingsStore.CreateForCurrentUser();
            _coordinator = new CaptureCoordinator();
            _dimensionStyleService = new DimensionStyleService();

            ScaleOptions = new[]
            {
                new Choice<double>("1×", 1),
                new Choice<double>("2×（推荐）", 2),
                new Choice<double>("3×", 3)
            };
            FormatOptions = new[]
            {
                new Choice<ImageOutputFormat>("PNG", ImageOutputFormat.Png),
                new Choice<ImageOutputFormat>("JPG", ImageOutputFormat.Jpeg)
            };
            ApplySettings(_settingsStore.Load());
            CaptureOriginalCommand = CreateCaptureCommand(ScreenshotColorMode.Original);
            CaptureBlackAndWhiteCommand = CreateCaptureCommand(ScreenshotColorMode.BlackAndWhite);
            CaptureWhiteBackgroundCommand = CreateCaptureCommand(ScreenshotColorMode.WhiteBackground);
            CaptureGrayscaleCommand = CreateCaptureCommand(ScreenshotColorMode.Grayscale);
            SaveCustomCommandCommand = CreateOperationCommand(
                SaveCustomCommandAsync,
                ReportToolFailure,
                () => IsCustomCommandValid);
            RefreshDimensionStylesCommand = CreateOperationCommand(
                RefreshDimensionStylesAsync,
                ReportToolFailure);
            ApplyDimensionStyleCommand = CreateOperationCommand(
                ApplyDimensionStyleAsync,
                ReportToolFailure,
                () => !string.IsNullOrWhiteSpace(SelectedDimensionStyle));
            Status = "选好清晰度，点击一种效果开始截图。";
        }

        public IReadOnlyList<Choice<double>> ScaleOptions { get; }

        public string VersionLabel => "v" + typeof(ScreenshotViewModel).Assembly.GetName().Version.ToString(3);

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetField(ref _isBusy, value))
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsIdle)));
                    foreach (ICommand command in new[]
                    {
                        CaptureOriginalCommand, CaptureBlackAndWhiteCommand,
                        CaptureWhiteBackgroundCommand, CaptureGrayscaleCommand,
                        SaveCustomCommandCommand, RefreshDimensionStylesCommand, ApplyDimensionStyleCommand
                    })
                    {
                        (command as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                    }
                }
            }
        }

        public bool IsIdle => !IsBusy;

        public IReadOnlyList<Choice<ImageOutputFormat>> FormatOptions { get; }

        public Choice<double> SelectedScale
        {
            get => _selectedScale;
            set
            {
                if (SetField(ref _selectedScale, value))
                {
                    UpdateOutputSizePreview();
                }
            }
        }

        public Choice<ImageOutputFormat> SelectedFormat
        {
            get => _selectedFormat;
            set => SetField(ref _selectedFormat, WhiteToTransparent ? FormatOptions.First(item => item.Value == ImageOutputFormat.Png) : value);
        }

        public Choice<ScreenshotColorMode> SelectedColorMode
        {
            get => _selectedColorMode;
            set => SetField(ref _selectedColorMode, value);
        }

        public bool WhiteToTransparent
        {
            get => _whiteToTransparent;
            set
            {
                if (SetField(ref _whiteToTransparent, value) && value)
                {
                    SelectedFormat = FormatOptions.First(item => item.Value == ImageOutputFormat.Png);
                }
            }
        }

        public int BorderPixels
        {
            get => _borderPixels;
            set => SetField(ref _borderPixels, Math.Max(0, value));
        }

        public bool CopyToClipboard
        {
            get => _copyToClipboard;
            set => SetField(ref _copyToClipboard, value);
        }

        public bool AutoSave
        {
            get => _autoSave;
            set => SetField(ref _autoSave, value);
        }

        public string OutputDirectory
        {
            get => _outputDirectory;
            set => SetField(ref _outputDirectory, value);
        }

        public string Status
        {
            get => _status;
            private set => SetField(ref _status, value);
        }

        public string PreviewWidth
        {
            get => _previewWidth;
            private set => SetField(ref _previewWidth, value);
        }

        public string PreviewHeight
        {
            get => _previewHeight;
            private set => SetField(ref _previewHeight, value);
        }

        public string CustomCommand
        {
            get => _customCommand;
            set
            {
                if (SetField(ref _customCommand, value))
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCustomCommandValid)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CustomCommandHint)));
                    (SaveCustomCommandCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsCustomCommandValid => !string.IsNullOrWhiteSpace(CustomCommand) &&
            string.Equals(ScreenshotSettings.NormalizeCommandName(CustomCommand, string.Empty),
                CustomCommand.Trim(), StringComparison.OrdinalIgnoreCase);

        public string CustomCommandHint => IsCustomCommandValid
            ? "以英文字母开头，最多 16 位；编辑后点击保存生效。"
            : "请输入有效命令：字母开头，只能含字母、数字、_ 或 -。";

        public string DimensionStylesHint => DimensionStyleOptions.Count == 0
            ? "暂无可用样式。请打开图纸后点击刷新。"
            : "已加载 " + DimensionStyleOptions.Count + " 个样式；切换图纸后请刷新。";

        public IReadOnlyList<string> DimensionStyleOptions
        {
            get => _dimensionStyleOptions;
            private set
            {
                if (SetField(ref _dimensionStyleOptions, value))
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DimensionStylesHint)));
                }
            }
        }

        public string SelectedDimensionStyle
        {
            get => _selectedDimensionStyle;
            set
            {
                if (SetField(ref _selectedDimensionStyle, value))
                {
                    (ApplyDimensionStyleCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool ModifySelectedBlockOnly
        {
            get => _modifySelectedBlockOnly;
            set
            {
                if (SetField(ref _modifySelectedBlockOnly, value))
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModifyAllSameNameBlocks)));
                }
            }
        }

        public bool ModifyAllSameNameBlocks
        {
            get => !ModifySelectedBlockOnly;
            set
            {
                if (value)
                {
                    ModifySelectedBlockOnly = false;
                }
            }
        }

        public ICommand CaptureOriginalCommand { get; }

        public ICommand CaptureBlackAndWhiteCommand { get; }

        public ICommand CaptureWhiteBackgroundCommand { get; }

        public ICommand CaptureGrayscaleCommand { get; }

        public ICommand SaveCustomCommandCommand { get; }

        public ICommand RefreshDimensionStylesCommand { get; }

        public ICommand ApplyDimensionStyleCommand { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        public async Task InitializeAsync()
        {
            try
            {
                await RunOperationAsync(() => LoadDimensionStylesAsync(false));
            }
            catch (Exception exception)
            {
                ReportToolFailure(exception);
            }
        }

        public void SaveSettings()
        {
            try
            {
                _settingsStore.Save(CreateSettings(
                    CaptureSourceMode.Region,
                    SelectedColorMode?.Value ?? ScreenshotColorMode.Original,
                    CustomCommandRegistry.RegisteredCommand));
            }
            catch (IOException)
            {
                // Closing AutoCAD must not be blocked by a locked settings file.
            }
            catch (UnauthorizedAccessException)
            {
                // Settings are optional; capture errors are reported separately.
            }
        }

        private ICommand CreateCaptureCommand(ScreenshotColorMode colorMode)
        {
            return CreateOperationCommand(
                () => CaptureAsync(CaptureSourceMode.Region, colorMode),
                ReportCaptureFailure);
        }

        private ICommand CreateOperationCommand(Func<Task> action, Action<Exception> onError, Func<bool> canExecute = null)
        {
            return new AsyncRelayCommand(
                () => RunOperationAsync(action),
                () => IsIdle && (canExecute == null || canExecute()),
                onError);
        }

        private async Task RunOperationAsync(Func<Task> action)
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                await action();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private Task SaveCustomCommandAsync()
        {
            string normalized = ScreenshotSettings.NormalizeCommandName(CustomCommand);
            if (!string.Equals(normalized, (CustomCommand ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
            {
                Status = "命令必须以字母开头，只能包含字母、数字、下划线或短横线，最长 16 个字符";
                return Task.CompletedTask;
            }

            string registered = CustomCommandRegistry.Register(normalized);
            CustomCommand = registered;
            ScreenshotSettings settings = CreateSettings(
                CaptureSourceMode.Region,
                SelectedColorMode?.Value ?? ScreenshotColorMode.Original,
                registered);
            _settingsStore.Save(settings);
            Status = "自定义命令已保存：" + registered + "（在 CAD 命令行输入即可打开截图窗口）";
            return Task.CompletedTask;
        }

        private Task RefreshDimensionStylesAsync()
        {
            return LoadDimensionStylesAsync(true);
        }

        private async Task LoadDimensionStylesAsync(bool reportStatus)
        {
            IReadOnlyList<string> styles = await _dimensionStyleService.GetStyleNamesAsync();
            string previous = SelectedDimensionStyle;
            DimensionStyleOptions = styles;
            SelectedDimensionStyle = styles.FirstOrDefault(
                name => string.Equals(name, previous, StringComparison.CurrentCultureIgnoreCase))
                ?? styles.FirstOrDefault();
            if (reportStatus) Status = DimensionStylesHint;
        }

        private async Task ApplyDimensionStyleAsync()
        {
            bool windowHidden = false;
            try
            {
                Status = "请选择标注和图形…";
                _window.Hide();
                windowHidden = true;
                Status = await _dimensionStyleService.ApplyAsync(
                    SelectedDimensionStyle,
                    ModifySelectedBlockOnly);
            }
            finally
            {
                if (windowHidden)
                {
                    _window.Dispatcher.Invoke(new Action(() =>
                    {
                        if (_window.IsClosed) return;
                        _window.Show();
                        _window.Activate();
                    }));
                }
            }
        }

        private async Task CaptureAsync(CaptureSourceMode mode, ScreenshotColorMode colorMode)
        {
            bool windowHidden = false;

            try
            {
                SelectedColorMode = new Choice<ScreenshotColorMode>(colorMode.ToString(), colorMode);
                ScreenshotSettings settings = CreateSettings(mode, colorMode, CustomCommandRegistry.RegisteredCommand);
                string settingsWarning = null;
                try
                {
                    _settingsStore.Save(settings);
                }
                catch (IOException exception)
                {
                    settingsWarning = "设置未保存：" + exception.Message;
                }
                catch (UnauthorizedAccessException exception)
                {
                    settingsWarning = "设置未保存：" + exception.Message;
                }
                Status = "请选择截图范围…";
                _window.Hide();
                windowHidden = true;

                CaptureOutcome outcome = await _coordinator.CaptureAsync(settings);
                Status = settingsWarning == null ? outcome.Message : outcome.Message + "；" + settingsWarning;
            }
            catch (Exception exception)
            {
                ReportCaptureFailure(exception);
            }
            finally
            {
                if (windowHidden)
                {
                    // The user may close the modeless window while the CAD command is active.
                    // Restore on the WPF dispatcher, and never reopen a closed Window.
                    try
                    {
                        _window.Dispatcher.Invoke(new Action(() =>
                        {
                            if (_window.IsClosed) return;
                            _window.Show();
                            _window.Activate();
                        }));
                    }
                    catch (Exception exception)
                    {
                        ReportCaptureFailure(exception);
                    }
                }
            }
        }

        private void ReportCaptureFailure(Exception exception)
        {
            string logPath = CaptureDiagnostics.TryWrite("截图界面", exception);
            string message = "截图失败：" + exception.Message +
                (logPath == null ? "；诊断日志写入失败" : "；日志：" + logPath);
            _window.Dispatcher.Invoke(new Action(() => Status = message));
        }

        private ScreenshotSettings CreateSettings(
            CaptureSourceMode mode,
            ScreenshotColorMode colorMode,
            string customCommand)
        {
            return new ScreenshotSettings
            {
                CaptureMode = mode,
                PixelScale = SelectedScale?.Value ?? 2,
                OutputFormat = ImageOutputFormat.Png,
                ColorMode = colorMode,
                WhiteToTransparent = false,
                BorderPixels = 0,
                CopyToClipboard = true,
                AutoSave = false,
                OutputDirectory = OutputDirectory,
                JpegQuality = 92,
                TargetLongEdge = 0,
                CustomCommand = customCommand
            }.Normalize();
        }

        private void ApplySettings(ScreenshotSettings settings)
        {
            ScreenshotSettings normalized = settings.Normalize();
            double preferredScale = normalized.TargetLongEdge > 0 ? 2 : normalized.PixelScale;
            SelectedScale = ScaleOptions
                .OrderBy(item => Math.Abs(item.Value - preferredScale))
                .First();
            SelectedFormat = FormatOptions.First(item => item.Value == normalized.OutputFormat);
            SelectedColorMode = new Choice<ScreenshotColorMode>(
                normalized.ColorMode.ToString(),
                normalized.ColorMode);
            WhiteToTransparent = normalized.WhiteToTransparent;
            BorderPixels = normalized.BorderPixels;
            CopyToClipboard = normalized.CopyToClipboard;
            AutoSave = normalized.AutoSave;
            OutputDirectory = normalized.OutputDirectory;
            CustomCommand = CustomCommandRegistry.RegisteredCommand ?? normalized.CustomCommand;
        }

        private void ReportToolFailure(Exception exception)
        {
            string logPath = CaptureDiagnostics.TryWrite("综合工具", exception);
            string message = "操作失败：" + exception.Message +
                (logPath == null ? "；诊断日志写入失败" : "；日志：" + logPath);
            _window.Dispatcher.Invoke(new Action(() => Status = message));
        }

        internal void UpdateOutputSizePreview()
        {
            try
            {
                PixelSize viewport = ScreenPixelMapper.MeasureViewport();
                PixelSize target = PixelScaleCalculator.Scale(
                    viewport,
                    SelectedScale?.Value ?? 2);
                PreviewWidth = target.Width.ToString();
                PreviewHeight = target.Height.ToString();
            }
            catch
            {
                PreviewWidth = "—";
                PreviewHeight = "—";
            }
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
