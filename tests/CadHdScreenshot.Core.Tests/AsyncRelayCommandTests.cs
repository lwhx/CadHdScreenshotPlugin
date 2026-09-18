using System;
using System.Threading.Tasks;
using CadHdScreenshot.Plugin;
using NUnit.Framework;

namespace CadHdScreenshot.Core.Tests
{
    [TestFixture]
    public sealed class AsyncRelayCommandTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public async Task ExecuteAsync_HandlesFailureAndReenablesCommand(bool asynchronous)
        {
            int attempts = 0;
            var expected = new InvalidOperationException("Window restore failure");
            Exception reported = null;
            var command = new AsyncRelayCommand(async () =>
            {
                attempts++;
                if (asynchronous) await Task.Yield();
                if (attempts == 1) throw expected;
            }, onError: ex => reported = ex);
            await command.ExecuteAsync(null);
            Assert.That(reported, Is.SameAs(expected));
            Assert.That(command.CanExecute(null), Is.True);
            await command.ExecuteAsync(null);
            Assert.That(attempts, Is.EqualTo(2));
        }

        [Test]
        public async Task ExecuteAsync_DoesNotEscapeWhenErrorReporterThrows()
        {
            var command = new AsyncRelayCommand(() => { throw new InvalidOperationException("capture"); },
                onError: ex => { throw new InvalidOperationException("reporter"); });
            await command.ExecuteAsync(null);
            Assert.That(command.CanExecute(null), Is.True);
        }

        [Test]
        public async Task ExecuteAsync_IgnoresDuplicateInvocationWhileBusy()
        {
            var release = new TaskCompletionSource<bool>();
            int attempts = 0;
            var command = new AsyncRelayCommand(() => { attempts++; return release.Task; });
            Task first = command.ExecuteAsync(null);
            Assert.That(command.CanExecute(null), Is.False);
            await command.ExecuteAsync(null);
            Assert.That(attempts, Is.EqualTo(1));
            release.SetResult(true);
            await first;
            Assert.That(command.CanExecute(null), Is.True);
        }

        [Test]
        public async Task ExecuteAsync_ContainsCanExecuteChangedHandlerFailure()
        {
            Exception reported = null;
            var command = new AsyncRelayCommand(() => Task.CompletedTask, onError: ex => reported = ex);
            command.CanExecuteChanged += (sender, args) => { throw new InvalidOperationException("UI event"); };
            await command.ExecuteAsync(null);
            Assert.That(reported, Is.TypeOf<InvalidOperationException>());
            Assert.That(command.CanExecute(null), Is.True);
        }
    }
}
