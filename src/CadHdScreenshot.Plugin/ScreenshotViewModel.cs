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

		private readonly MirroredBlockTextService _mirroredBlockTextService;

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

		public IReadOnlyList<Choice<double>> ScaleOptions { get; }

		public IReadOnlyList<Choice<ImageOutputFormat>> FormatOptions { get; }

		public Choice<double> SelectedScale
		{
			get
			{
				return _selectedScale;
			}
			set
			{
				if (SetField(ref _selectedScale, value, "SelectedScale"))
				{
					UpdateOutputSizePreview();
				}
			}
		}

		public Choice<ImageOutputFormat> SelectedFormat
		{
			get
			{
				return _selectedFormat;
			}
			set
			{
				SetField(ref _selectedFormat, WhiteToTransparent ? FormatOptions.First((Choice<ImageOutputFormat> item) => item.Value == ImageOutputFormat.Png) : value, "SelectedFormat");
			}
		}

		public Choice<ScreenshotColorMode> SelectedColorMode
		{
			get
			{
				return _selectedColorMode;
			}
			set
			{
				SetField(ref _selectedColorMode, value, "SelectedColorMode");
			}
		}

		public bool WhiteToTransparent
		{
			get
			{
				return _whiteToTransparent;
			}
			set
			{
				if (SetField(ref _whiteToTransparent, value, "WhiteToTransparent") & value)
				{
					SelectedFormat = FormatOptions.First((Choice<ImageOutputFormat> item) => item.Value == ImageOutputFormat.Png);
				}
			}
		}

		public int BorderPixels
		{
			get
			{
				return _borderPixels;
			}
			set
			{
				SetField(ref _borderPixels, Math.Max(0, value), "BorderPixels");
			}
		}

		public bool CopyToClipboard
		{
			get
			{
				return _copyToClipboard;
			}
			set
			{
				SetField(ref _copyToClipboard, value, "CopyToClipboard");
			}
		}

		public bool AutoSave
		{
			get
			{
				return _autoSave;
			}
			set
			{
				SetField(ref _autoSave, value, "AutoSave");
			}
		}

		public string OutputDirectory
		{
			get
			{
				return _outputDirectory;
			}
			set
			{
				SetField(ref _outputDirectory, value, "OutputDirectory");
			}
		}

		public string Status
		{
			get
			{
				return _status;
			}
			private set
			{
				SetField(ref _status, value, "Status");
			}
		}

		public string PreviewWidth
		{
			get
			{
				return _previewWidth;
			}
			private set
			{
				SetField(ref _previewWidth, value, "PreviewWidth");
			}
		}

		public string PreviewHeight
		{
			get
			{
				return _previewHeight;
			}
			private set
			{
				SetField(ref _previewHeight, value, "PreviewHeight");
			}
		}

		public string CustomCommand
		{
			get
			{
				return _customCommand;
			}
			set
			{
				SetField(ref _customCommand, value, "CustomCommand");
			}
		}

		public IReadOnlyList<string> DimensionStyleOptions
		{
			get
			{
				return _dimensionStyleOptions;
			}
			private set
			{
				SetField(ref _dimensionStyleOptions, value, "DimensionStyleOptions");
			}
		}

		public string SelectedDimensionStyle
		{
			get
			{
				return _selectedDimensionStyle;
			}
			set
			{
				if (SetField(ref _selectedDimensionStyle, value, "SelectedDimensionStyle"))
				{
					(ApplyDimensionStyleCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
				}
			}
		}

		public bool ModifySelectedBlockOnly
		{
			get
			{
				return _modifySelectedBlockOnly;
			}
			set
			{
				if (SetField(ref _modifySelectedBlockOnly, value, "ModifySelectedBlockOnly"))
				{
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ModifyAllSameNameBlocks"));
				}
			}
		}

		public bool ModifyAllSameNameBlocks
		{
			get
			{
				return !ModifySelectedBlockOnly;
			}
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

		public ICommand FixMirroredBlockTextCommand { get; }

		public event PropertyChangedEventHandler PropertyChanged;

		public ScreenshotViewModel(ScreenshotWindow window)
		{
			_window = window ?? throw new ArgumentNullException("window");
			_settingsStore = FileSettingsStore.CreateForCurrentUser();
			_coordinator = new CaptureCoordinator();
			_dimensionStyleService = new DimensionStyleService();
			_mirroredBlockTextService = new MirroredBlockTextService();
			ScaleOptions = new Choice<double>[3]
			{
				new Choice<double>("1×", 1.0),
				new Choice<double>("2×（推荐）", 2.0),
				new Choice<double>("3×", 3.0)
			};
			FormatOptions = new Choice<ImageOutputFormat>[2]
			{
				new Choice<ImageOutputFormat>("PNG", ImageOutputFormat.Png),
				new Choice<ImageOutputFormat>("JPG", ImageOutputFormat.Jpeg)
			};
			ApplySettings(_settingsStore.Load());
			CaptureOriginalCommand = CreateCaptureCommand(ScreenshotColorMode.Original);
			CaptureBlackAndWhiteCommand = CreateCaptureCommand(ScreenshotColorMode.BlackAndWhite);
			CaptureWhiteBackgroundCommand = CreateCaptureCommand(ScreenshotColorMode.WhiteBackground);
			CaptureGrayscaleCommand = CreateCaptureCommand(ScreenshotColorMode.Grayscale);
			SaveCustomCommandCommand = new AsyncRelayCommand(SaveCustomCommandAsync, null, ReportCaptureFailure);
			RefreshDimensionStylesCommand = new AsyncRelayCommand(RefreshDimensionStylesAsync, null, ReportToolFailure);
			ApplyDimensionStyleCommand = new AsyncRelayCommand(ApplyDimensionStyleAsync, () => !string.IsNullOrWhiteSpace(SelectedDimensionStyle), ReportToolFailure);
			FixMirroredBlockTextCommand = new AsyncRelayCommand(FixMirroredBlockTextAsync, null, ReportToolFailure);
			Status = "就绪（" + typeof(ScreenshotViewModel).Assembly.GetName().Version.ToString(3) + "）";
		}

		public async Task InitializeAsync()
		{
			try
			{
				await RefreshDimensionStylesAsync();
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
				_settingsStore.Save(CreateSettings(CaptureSourceMode.Region, SelectedColorMode?.Value ?? ScreenshotColorMode.Original, CustomCommand));
			}
			catch (IOException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}
		}

		private ICommand CreateCaptureCommand(ScreenshotColorMode colorMode)
		{
			return new AsyncRelayCommand(() => CaptureAsync(CaptureSourceMode.Region, colorMode), null, ReportCaptureFailure);
		}

		private Task SaveCustomCommandAsync()
		{
			string text = ScreenshotSettings.NormalizeCommandName(CustomCommand);
			if (!string.Equals(text, (CustomCommand ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
			{
				Status = "命令必须以字母开头，只能包含字母、数字、下划线或短横线，最长 16 个字符";
				return Task.CompletedTask;
			}
			string text2 = (CustomCommand = CustomCommandRegistry.Register(text));
			ScreenshotSettings settings = CreateSettings(CaptureSourceMode.Region, SelectedColorMode?.Value ?? ScreenshotColorMode.Original, text2);
			_settingsStore.Save(settings);
			Status = "自定义命令已保存：" + text2 + "（在 CAD 命令行输入即可打开截图窗口）";
			return Task.CompletedTask;
		}

		private async Task RefreshDimensionStylesAsync()
		{
			IReadOnlyList<string> readOnlyList = await _dimensionStyleService.GetStyleNamesAsync();
			string previous = SelectedDimensionStyle;
			DimensionStyleOptions = readOnlyList;
			SelectedDimensionStyle = readOnlyList.FirstOrDefault((string name) => string.Equals(name, previous, StringComparison.CurrentCultureIgnoreCase)) ?? readOnlyList.FirstOrDefault();
			Status = ((readOnlyList.Count == 0) ? "当前图纸没有可用的标注样式" : ("已加载 " + readOnlyList.Count + " 个标注样式"));
		}

		private async Task ApplyDimensionStyleAsync()
		{
			bool windowHidden = false;
			try
			{
				Status = "请选择标注和图形…";
				_window.Hide();
				windowHidden = true;
				Status = await _dimensionStyleService.ApplyAsync(SelectedDimensionStyle, ModifySelectedBlockOnly);
			}
			finally
			{
				if (windowHidden)
				{
					_window.Dispatcher.Invoke(delegate
					{
						if (!_window.IsClosed)
						{
							_window.Show();
							_window.Activate();
						}
					});
				}
			}
		}

		private async Task FixMirroredBlockTextAsync()
		{
			bool windowHidden = false;
			try
			{
				Status = "请选择文字反向的镜像块…";
				_window.Hide();
				windowHidden = true;
				Status = await _mirroredBlockTextService.FixAsync();
			}
			finally
			{
				if (windowHidden)
				{
					_window.Dispatcher.Invoke(delegate
					{
						if (!_window.IsClosed)
						{
							_window.Show();
							_window.Activate();
						}
					});
				}
			}
		}

		private async Task CaptureAsync(CaptureSourceMode mode, ScreenshotColorMode colorMode)
		{
			bool windowHidden = false;
			try
			{
				SelectedColorMode = new Choice<ScreenshotColorMode>(colorMode.ToString(), colorMode);
				ScreenshotSettings settings = CreateSettings(mode, colorMode, CustomCommand);
				string settingsWarning = null;
				try
				{
					_settingsStore.Save(settings);
				}
				catch (IOException ex)
				{
					settingsWarning = "设置未保存：" + ex.Message;
				}
				catch (UnauthorizedAccessException ex2)
				{
					settingsWarning = "设置未保存：" + ex2.Message;
				}
				Status = "请选择截图范围…";
				_window.Hide();
				windowHidden = true;
				CaptureOutcome captureOutcome = await _coordinator.CaptureAsync(settings);
				Status = ((settingsWarning == null) ? captureOutcome.Message : (captureOutcome.Message + "；" + settingsWarning));
			}
			catch (Exception exception)
			{
				ReportCaptureFailure(exception);
			}
			finally
			{
				if (windowHidden)
				{
					try
					{
						_window.Dispatcher.Invoke(delegate
						{
							if (!_window.IsClosed)
							{
								_window.Show();
								_window.Activate();
							}
						});
					}
					catch (Exception exception2)
					{
						ReportCaptureFailure(exception2);
					}
				}
			}
		}

		private void ReportCaptureFailure(Exception exception)
		{
			string text = CaptureDiagnostics.TryWrite("截图界面", exception);
			string message = "截图失败：" + exception.Message + ((text == null) ? "；诊断日志写入失败" : ("；日志：" + text));
			_window.Dispatcher.Invoke(delegate
			{
				Status = message;
			});
		}

		private ScreenshotSettings CreateSettings(CaptureSourceMode mode, ScreenshotColorMode colorMode, string customCommand)
		{
			return new ScreenshotSettings
			{
				CaptureMode = mode,
				PixelScale = (SelectedScale?.Value ?? 2.0),
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
			double preferredScale = ((normalized.TargetLongEdge > 0) ? 2.0 : normalized.PixelScale);
			SelectedScale = ScaleOptions.OrderBy((Choice<double> item) => Math.Abs(item.Value - preferredScale)).First();
			SelectedFormat = FormatOptions.First((Choice<ImageOutputFormat> item) => item.Value == normalized.OutputFormat);
			SelectedColorMode = new Choice<ScreenshotColorMode>(normalized.ColorMode.ToString(), normalized.ColorMode);
			WhiteToTransparent = normalized.WhiteToTransparent;
			BorderPixels = normalized.BorderPixels;
			CopyToClipboard = normalized.CopyToClipboard;
			AutoSave = normalized.AutoSave;
			OutputDirectory = normalized.OutputDirectory;
			CustomCommand = CustomCommandRegistry.RegisteredCommand ?? normalized.CustomCommand;
		}

		private void ReportToolFailure(Exception exception)
		{
			string text = CaptureDiagnostics.TryWrite("综合工具", exception);
			string message = "操作失败：" + exception.Message + ((text == null) ? "；诊断日志写入失败" : ("；日志：" + text));
			_window.Dispatcher.Invoke(delegate
			{
				Status = message;
			});
		}

		private void UpdateOutputSizePreview()
		{
			try
			{
				PixelSize pixelSize = PixelScaleCalculator.Scale(ScreenPixelMapper.MeasureViewport(), SelectedScale?.Value ?? 2.0);
				PreviewWidth = pixelSize.Width.ToString();
				PreviewHeight = pixelSize.Height.ToString();
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
