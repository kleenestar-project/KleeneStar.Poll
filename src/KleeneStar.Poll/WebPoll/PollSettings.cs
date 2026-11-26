using Microsoft.Extensions.Configuration;
using System;
using System.Globalization;

namespace KleeneStar.Poll.WebPoll
{
    /// <summary>
    /// The settings of the poll plugin, read from <c>Plugins:kleenestar.poll</c>.
    /// </summary>
    /// <remarks>
    /// Every value has a default, so the plugin runs without any settings of its own:
    /// <code>
    /// "Plugins": {
    ///   "kleenestar.poll": {
    ///     "TimeZone": "Europe/Berlin",
    ///     "AutoCloseInterval": 5,
    ///     "RunningCount": 10,
    ///     "DefaultDuration": 60
    ///   }
    /// }
    /// </code>
    /// </remarks>
    public sealed class PollSettings
    {
        /// <summary>
        /// Gets the zone dates are proposed in when the account names none; <see langword="null"/>
        /// for the zone of the host.
        /// </summary>
        public string TimeZone { get; init; }

        /// <summary>
        /// Gets how often, in minutes, the polls whose deadline ran out are closed. Zero switches
        /// the closing off.
        /// </summary>
        public int AutoCloseInterval { get; init; } = 5;

        /// <summary>
        /// Gets how many running polls the sidebar lists at most.
        /// </summary>
        public int RunningCount { get; init; } = 10;

        /// <summary>
        /// Gets how long, in minutes, a proposed date lasts when only its start is given.
        /// </summary>
        public int DefaultDuration { get; init; } = 60;

        /// <summary>
        /// Reads the settings.
        /// </summary>
        /// <param name="configuration">The section of the plugin, or <see langword="null"/>.</param>
        /// <returns>The settings; the defaults for what is missing or not a number.</returns>
        public static PollSettings From(IConfiguration configuration)
        {
            var defaults = new PollSettings();

            if (configuration is null)
            {
                return defaults;
            }

            return new PollSettings
            {
                TimeZone = string.IsNullOrWhiteSpace(configuration["TimeZone"]) ? null : configuration["TimeZone"].Trim(),
                AutoCloseInterval = Number(configuration["AutoCloseInterval"], defaults.AutoCloseInterval, 0, 24 * 60),
                RunningCount = Number(configuration["RunningCount"], defaults.RunningCount, 1, 100),
                DefaultDuration = Number(configuration["DefaultDuration"], defaults.DefaultDuration, 0, 7 * 24 * 60)
            };
        }

        /// <summary>
        /// Reads a whole number within bounds.
        /// </summary>
        /// <param name="value">The configured value.</param>
        /// <param name="fallback">The default.</param>
        /// <param name="min">The smallest value allowed.</param>
        /// <param name="max">The largest value allowed.</param>
        /// <returns>The number.</returns>
        private static int Number(string value, int fallback, int min, int max)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                ? Math.Clamp(number, min, max)
                : fallback;
        }
    }
}
