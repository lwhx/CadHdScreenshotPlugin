using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CadHdScreenshot.Plugin
{
	internal sealed class AsyncRelayCommand : ICommand
	{
		private readonly Func<Task> _execute;

		private readonly Func<bool> _canExecute;

		private readonly Action<Exception> _onError;

		private bool _isExecuting;

		public event EventHandler CanExecuteChanged;

		public AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute = null, Action<Exception> onError = null)
		{
			_execute = execute ?? throw new ArgumentNullException("execute");
			_canExecute = canExecute;
			_onError = onError;
		}

		public bool CanExecute(object parameter)
		{
			if (!_isExecuting)
			{
				if (_canExecute != null)
				{
					return _canExecute();
				}
				return true;
			}
			return false;
		}

		public async void Execute(object parameter)
		{
			await ExecuteAsync(parameter);
		}

		internal async Task ExecuteAsync(object parameter)
		{
			bool started = false;
			try
			{
				if (CanExecute(parameter))
				{
					_isExecuting = true;
					started = true;
					RaiseCanExecuteChanged();
					await _execute();
				}
			}
			catch (Exception exception)
			{
				ReportError(exception);
			}
			finally
			{
				if (started)
				{
					_isExecuting = false;
					RaiseCanExecuteChanged();
				}
			}
		}

		public void RaiseCanExecuteChanged()
		{
			try
			{
				CanExecuteChanged?.Invoke(this, EventArgs.Empty);
			}
			catch (Exception exception)
			{
				ReportError(exception);
			}
		}

		private void ReportError(Exception exception)
		{
			try
			{
				if (_onError == null)
				{
					CaptureDiagnostics.TryWrite("异步按钮", exception);
				}
				else
				{
					_onError(exception);
				}
			}
			catch (Exception ex)
			{
				CaptureDiagnostics.TryWrite("错误提示失败", new AggregateException(exception, ex));
			}
		}
	}
}
