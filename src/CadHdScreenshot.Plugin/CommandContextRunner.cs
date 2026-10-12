using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace CadHdScreenshot.Plugin
{
	internal static class CommandContextRunner
	{
		public static async Task<T> ExecuteAsync<T>(Func<Func<object, Task>, object, Task> dispatch, Func<T> action)
		{
			if (dispatch == null)
			{
				throw new ArgumentNullException("dispatch");
			}
			if (action == null)
			{
				throw new ArgumentNullException("action");
			}
			T result = default(T);
			ExceptionDispatchInfo failure = null;
			await dispatch(delegate
			{
				try
				{
					result = action();
				}
				catch (Exception source)
				{
					failure = ExceptionDispatchInfo.Capture(source);
				}
				return Task.CompletedTask;
			}, null);
			failure?.Throw();
			return result;
		}
	}
}
