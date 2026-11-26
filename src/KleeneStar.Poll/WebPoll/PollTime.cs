using KleeneStar.Model.Entities;
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace KleeneStar.Poll.WebPoll
{
    /// <summary>
    /// Time zones, conversions and the written form of times in the polls: dates are stored in
    /// UTC and shown and entered in the zone of the reader.
    /// </summary>
    public static partial class PollTime
    {
        /// <summary>
        /// Resolves a zone by a .NET or IANA id; the host's zone for an unknown or missing one.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns>The zone.</returns>
        public static TimeZoneInfo Zone(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return TimeZoneInfo.Local;
            }

            var trimmed = id.Trim();

            if (string.Equals(trimmed, "UTC", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "Etc/UTC", StringComparison.OrdinalIgnoreCase))
            {
                return TimeZoneInfo.Utc;
            }

            if (TimeZoneInfo.TryFindSystemTimeZoneById(trimmed, out var zone))
            {
                return zone;
            }

            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(trimmed, out var windowsId)
                && TimeZoneInfo.TryFindSystemTimeZoneById(windowsId, out zone))
            {
                return zone;
            }

            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(trimmed, out var ianaId)
                && TimeZoneInfo.TryFindSystemTimeZoneById(ianaId, out zone))
            {
                return zone;
            }

            return TimeZoneInfo.Local;
        }

        /// <summary>
        /// Converts a wall-clock time of a zone to UTC. A time the clocks skip is read with the
        /// offset before the jump; an ambiguous one as the earlier instant.
        /// </summary>
        /// <param name="local">The wall-clock time.</param>
        /// <param name="zone">The zone.</param>
        /// <returns>The instant in UTC.</returns>
        public static DateTime ToUtc(DateTime local, TimeZoneInfo zone)
        {
            zone ??= TimeZoneInfo.Local;

            var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

            if (zone.IsInvalidTime(unspecified))
            {
                var before = zone.GetUtcOffset(unspecified.AddHours(-6));

                return DateTime.SpecifyKind(unspecified - before, DateTimeKind.Utc);
            }

            if (zone.IsAmbiguousTime(unspecified))
            {
                var offsets = zone.GetAmbiguousTimeOffsets(unspecified);
                var earlier = offsets[0] > offsets[^1] ? offsets[0] : offsets[^1];

                return DateTime.SpecifyKind(unspecified - earlier, DateTimeKind.Utc);
            }

            return TimeZoneInfo.ConvertTimeToUtc(unspecified, zone);
        }

        /// <summary>
        /// Converts an instant to the wall-clock time of a zone.
        /// </summary>
        /// <param name="utc">The instant in UTC.</param>
        /// <param name="zone">The zone.</param>
        /// <returns>The wall-clock time.</returns>
        public static DateTime ToLocal(DateTime utc, TimeZoneInfo zone)
        {
            zone ??= TimeZoneInfo.Local;

            var instant = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

            return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(instant, zone), DateTimeKind.Unspecified);
        }

        /// <summary>
        /// Reads a time of day (<c>14:30</c>, <c>14.30</c>, <c>9:05</c>).
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="time">The time.</param>
        /// <returns><see langword="true"/> when a time was read.</returns>
        public static bool TryParseTime(string text, out TimeSpan time)
        {
            time = default;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var match = TimePattern().Match(text.Trim());

            if (!match.Success)
            {
                return false;
            }

            var hours = int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture);
            var minutes = int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture);

            if (hours > 23 || minutes > 59)
            {
                return false;
            }

            time = new TimeSpan(hours, minutes, 0);

            return true;
        }

        /// <summary>
        /// Reads a day (<c>25.11.2026</c> in the culture of the request, or <c>2026-11-25</c>).
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="day">The day.</param>
        /// <returns><see langword="true"/> when a day was read.</returns>
        public static bool TryParseDay(string text, CultureInfo culture, out DateTime day)
        {
            day = default;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var trimmed = text.Trim();

            if (DateTime.TryParseExact(trimmed, ["yyyy-MM-dd", "yyyy-M-d"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso))
            {
                day = DateTime.SpecifyKind(iso.Date, DateTimeKind.Unspecified);

                return true;
            }

            if (!Core.WebControl.FieldValueForm.TryParseDate(trimmed, culture ?? CultureInfo.CurrentCulture, out var date))
            {
                return false;
            }

            day = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);

            return true;
        }

        /// <summary>
        /// Writes a proposed date in the zone and culture of a reader, for instance
        /// <c>Wed, 11/25/2026 2:30 PM – 3:30 PM</c> or, for whole days, <c>Wed, 11/25/2026</c>.
        /// </summary>
        /// <param name="option">The option.</param>
        /// <param name="zone">The zone.</param>
        /// <param name="culture">The culture.</param>
        /// <returns>The text, or <see langword="null"/> for an option without date.</returns>
        public static string FormatDate(PollOption option, TimeZoneInfo zone, CultureInfo culture)
        {
            if (option?.Start is not DateTime start)
            {
                return null;
            }

            culture ??= CultureInfo.CurrentCulture;

            var localStart = ToLocal(start, zone);
            var day = culture.DateTimeFormat.AbbreviatedDayNames[(int)localStart.DayOfWeek];
            var date = culture.DateTimeFormat.ShortDatePattern;
            var time = culture.DateTimeFormat.ShortTimePattern;

            if (option.AllDay)
            {
                var lastDay = option.End is DateTime allDayEnd && ToLocal(allDayEnd, zone) > localStart
                    ? ToLocal(allDayEnd, zone).AddTicks(-1).Date
                    : localStart.Date;

                return lastDay <= localStart.Date
                    ? $"{day}, {localStart.ToString(date, culture)}"
                    : $"{day}, {localStart.ToString(date, culture)} – {lastDay.ToString(date, culture)}";
            }

            if (option.End is not DateTime end || end == start)
            {
                return $"{day}, {localStart.ToString(date, culture)} {localStart.ToString(time, culture)}";
            }

            var localEnd = ToLocal(end, zone);

            return localEnd.Date == localStart.Date
                ? $"{day}, {localStart.ToString(date, culture)} {localStart.ToString(time, culture)} – {localEnd.ToString(time, culture)}"
                : $"{day}, {localStart.ToString(date, culture)} {localStart.ToString(time, culture)} – {localEnd.ToString(date, culture)} {localEnd.ToString(time, culture)}";
        }

        /// <summary>
        /// Writes an option as a reader sees it: the date (if any) and the text.
        /// </summary>
        /// <param name="option">The option.</param>
        /// <param name="zone">The zone.</param>
        /// <param name="culture">The culture.</param>
        /// <returns>The text.</returns>
        public static string Describe(PollOption option, TimeZoneInfo zone, CultureInfo culture)
        {
            if (option is null)
            {
                return string.Empty;
            }

            var date = FormatDate(option, zone, culture);

            if (date is null)
            {
                return option.Text ?? string.Empty;
            }

            return string.IsNullOrWhiteSpace(option.Text) ? date : $"{date} · {option.Text}";
        }

        [GeneratedRegex("^(?<h>[0-9]{1,2})[:.](?<m>[0-9]{2})$")]
        private static partial Regex TimePattern();
    }
}
