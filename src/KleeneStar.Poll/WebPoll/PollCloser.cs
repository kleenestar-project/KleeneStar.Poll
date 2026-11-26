using System;
using System.Threading;

namespace KleeneStar.Poll.WebPoll
{
    /// <summary>
    /// Closes, at a fixed interval, the polls whose deadline ran out
    /// (<see cref="WebManager.IPollManager.CloseExpired"/>).
    /// </summary>
    /// <remarks>
    /// The closing is idempotent - a poll is closed once, a second replica finds it closed - so
    /// several replicas may run it at the same time.
    /// </remarks>
    public sealed class PollCloser : IDisposable
    {
        private readonly Timer _timer;
        private int _running;

        /// <summary>
        /// Initializes a new instance of the class and starts the timer.
        /// </summary>
        /// <param name="interval">The interval.</param>
        public PollCloser(TimeSpan interval)
        {
            var period = interval < TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : interval;

            // the first round runs shortly after the start, so what ran out while the host was
            // down is closed without waiting a whole interval
            _timer = new Timer(_ => Tick(DateTime.UtcNow), null, TimeSpan.FromSeconds(30), period);
        }

        /// <summary>
        /// Runs one round, unless the previous one still runs.
        /// </summary>
        /// <param name="now">The present (UTC).</param>
        /// <returns>The number of polls closed.</returns>
        public int Tick(DateTime now)
        {
            if (Interlocked.Exchange(ref _running, 1) == 1)
            {
                return 0;
            }

            try
            {
                return PollHub.PollManager?.CloseExpired(now) ?? 0;
            }
            catch (Exception ex)
            {
                // the timer thread has nobody to report to but the log
                PollHub.ComponentHub?.LogManager?.DefaultLog?.Exception(ex);

                return 0;
            }
            finally
            {
                Volatile.Write(ref _running, 0);
            }
        }

        /// <summary>
        /// Stops the timer.
        /// </summary>
        public void Dispose()
        {
            _timer.Dispose();
        }
    }
}
