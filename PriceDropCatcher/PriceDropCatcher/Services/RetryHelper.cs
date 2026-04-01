using System;
using System.Threading;
using System.Threading.Tasks;

namespace PriceDropCatcher.Services
{
    internal static class RetryHelper
    {
        public static async Task<T> RunAsync<T>(Func<Task<T>> action, int attempts = 3, CancellationToken ct = default)
        {
            Exception last = null;
            for (var i = 0; i < attempts; i++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    return await action().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    last = ex;
                    if (i == attempts - 1) break;
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, i));
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }
            }
            throw last ?? new InvalidOperationException("Retry failed.");
        }

        public static async Task RunAsync(Func<Task> action, int attempts = 3, CancellationToken ct = default)
        {
            await RunAsync(async () =>
            {
                await action().ConfigureAwait(false);
                return true;
            }, attempts, ct).ConfigureAwait(false);
        }
    }
}
