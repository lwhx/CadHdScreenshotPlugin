using System;
using System.Threading.Tasks;
using CadHdScreenshot.Plugin;
using NUnit.Framework;

namespace CadHdScreenshot.Core.Tests
{
    [TestFixture]
    public sealed class CommandContextRunnerTests
    {
        [TestCase("selection")]
        [TestCase("rendering")]
        [TestCase("view restoration")]
        public void ExecuteAsync_DoesNotExposeCaptureExceptionToHost(string stage)
        {
            var expected = new InvalidOperationException(stage);
            Exception escapedIntoHost = null;
            Task callbackTask = null;
            bool hostReturned = false;

            Func<Func<object, Task>, object, Task> host = (callback, data) =>
            {
                try { callbackTask = callback(data); }
                catch (Exception ex) { escapedIntoHost = ex; }
                hostReturned = true;
                return Task.CompletedTask;
            };

            Exception surfacedAtCaller = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await CommandContextRunner.ExecuteAsync<int>(host, () => { throw expected; }));

            Assert.Multiple(() =>
            {
                Assert.That(escapedIntoHost, Is.Null, "AutoCAD must never receive an unhandled callback exception.");
                Assert.That(callbackTask?.Status, Is.EqualTo(TaskStatus.RanToCompletion));
                Assert.That(hostReturned, Is.True);
                Assert.That(surfacedAtCaller, Is.SameAs(expected));
                Assert.That(surfacedAtCaller.StackTrace, Does.Contain(nameof(CommandContextRunnerTests)));
            });
        }

        [Test]
        public async Task ExecuteAsync_ReturnsResultAfterHostCompletes()
        {
            var release = new TaskCompletionSource<bool>();
            Task<int> result = CommandContextRunner.ExecuteAsync<int>(async (callback, data) =>
            {
                await callback(data);
                await release.Task;
            }, () => 42);
            Assert.That(result.IsCompleted, Is.False);
            release.SetResult(true);
            Assert.That(await result, Is.EqualTo(42));
        }

        [Test]
        public void ExecuteAsync_PropagatesDispatchFailureToCaller()
        {
            var expected = new InvalidOperationException("Unable to enter command context");
            bool actionRan = false;
            Assert.That(Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await CommandContextRunner.ExecuteAsync<int>((callback, data) => { throw expected; },
                    () => { actionRan = true; return 1; })), Is.SameAs(expected));
            Assert.That(actionRan, Is.False);
        }

        [Test]
        public async Task ExecuteAsync_AllowsAnotherCaptureAfterFailure()
        {
            Func<Func<object, Task>, object, Task> host = (callback, data) => callback(data);
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await CommandContextRunner.ExecuteAsync<int>(host, () => { throw new InvalidOperationException(); }));
            Assert.That(await CommandContextRunner.ExecuteAsync(host, () => 7), Is.EqualTo(7));
        }
    }
}
