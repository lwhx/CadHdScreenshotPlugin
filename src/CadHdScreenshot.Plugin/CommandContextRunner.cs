using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace CadHdScreenshot.Plugin
{
    internal static class CommandContextRunner
    {
        public static async Task<T> ExecuteAsync<T>(
            Func<Func<object, Task>, object, Task> dispatch,
            Func<T> action)
        {
            if (dispatch == null) throw new ArgumentNullException(nameof(dispatch));
            if (action == null) throw new ArgumentNullException(nameof(action));

            T result = default(T);
            ExceptionDispatchInfo failure = null;
            await dispatch(data =>
            {
                // Never throw (or return a faulted Task) across AutoCAD's native callback.
                try
                {
                    result = action();
                }
                catch (Exception exception)
                {
                    failure = ExceptionDispatchInfo.Capture(exception);
                }
                return Task.CompletedTask;
            }, null);

            // Re-throw only after AutoCAD has returned to our managed caller.
            failure?.Throw();
            return result;
        }
    }
}
