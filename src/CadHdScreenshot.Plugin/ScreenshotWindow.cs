using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Markup;

namespace CadHdScreenshot.Plugin
{
	public partial class ScreenshotWindow : Window, IComponentConnector
	{
		private readonly ScreenshotViewModel _viewModel;

		internal bool IsClosed { get; private set; }

		public ScreenshotWindow()
		{
			InitializeComponent();
			_viewModel = new ScreenshotViewModel(this);
			base.DataContext = _viewModel;
			InputMethodPanel.DataContext = InputMethod.InputMethodService.Current;
		}

		internal void SelectInputMethodTab() { InputMethodTab.IsSelected = true; }

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			InputMethod.InputMethodService.Current.PanelHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
		}

		protected override void OnClosed(EventArgs e)
		{
			IsClosed = true;
			InputMethod.InputMethodService.Current.PanelHandle = IntPtr.Zero;
			base.OnClosed(e);
		}

		protected override async void OnContentRendered(EventArgs e)
		{
			base.OnContentRendered(e);
			await _viewModel.InitializeAsync();
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			_viewModel.SaveSettings();
			base.OnClosing(e);
		}
	}
}
