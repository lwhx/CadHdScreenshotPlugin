using System.ComponentModel;
using System.Windows;

namespace CadHdScreenshot.Plugin
{
    public partial class ScreenshotWindow : Window
    {
        private readonly ScreenshotViewModel _viewModel;

        public ScreenshotWindow()
        {
            InitializeComponent();
            _viewModel = new ScreenshotViewModel(this);
            DataContext = _viewModel;
        }

        internal bool IsClosed { get; private set; }

        protected override void OnClosed(System.EventArgs e)
        {
            IsClosed = true;
            base.OnClosed(e);
        }

        protected override async void OnContentRendered(System.EventArgs e)
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
