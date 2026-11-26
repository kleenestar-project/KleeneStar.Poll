using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace KleeneStar.Poll.WebPoll
{
    /// <summary>
    /// The options of a poll as the dialogs write them: one option per line. A choice of a
    /// decision is its text; a proposed date of a scheduling poll is written as
    /// <list type="bullet">
    ///   <item><c>25.11.2026 14:00 - 15:30</c> - a time span on one day,</item>
    ///   <item><c>25.11.2026 14:00</c> - a start, lasting the default duration,</item>
    ///   <item><c>25.11.2026</c> or <c>25.11.2026 - 27.11.2026</c> - whole days,</item>
    ///   <item><c>25.11.2026 22:00 - 26.11.2026 02:00</c> - a span across midnight,</item>
    /// </list>
    /// with the day in the culture of the reader or as <c>2026-11-25</c>, and optionally a remark
    /// after a bar (<c>25.11.2026 14:00 - 15:30 | room 4.12</c>). Times are wall-clock times of
    /// the reader's zone.
    /// </summary>
    public static partial class PollOptionText
    {
        /// <summary>
        /// The most options a poll offers.
        /// </summary>
        public const int MaxOptions = 50;

        /// <summary>
        /// The longest text of an option.
        /// </summary>
        public const int MaxText = 512;

        /// <summary>
        /// Reads the options of a dialog.
        /// </summary>
        /// <param name="text">The lines.</param>
        /// <param name="dated">Whether the options are proposed dates.</param>
        /// <param name="zone">The zone the times are written in.</param>
        /// <param name="culture">The culture the days are written in.</param>
        /// <param name="duration">How long a date lasts that names its start only.</param>
        /// <param name="errors">Receives the lines that could not be read, one message each:
        /// the line number and the line.</param>
        /// <returns>The options read, in their order, without duplicates.</returns>
        public static IReadOnlyList<PollOption> Parse(string text, bool dated, TimeZoneInfo zone, CultureInfo culture, TimeSpan duration, ICollection<string> errors)
        {
            var result = new List<PollOption>();
            var lines = (text ?? string.Empty).Replace("\r\n", "\n").Split('\n');

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                if (line.Length == 0)
                {
                    continue;
                }

                var option = dated ? ParseDate(line, zone, culture, duration) : ParseChoice(line);

                if (option is null)
                {
                    errors?.Add($"{i + 1}: {line}");

                    continue;
                }

                if (!result.Any(x => Same(x, option)))
                {
                    result.Add(option);
                }
            }

            return result;
        }

        /// <summary>
        /// Writes the options as the dialogs show them, one per line, so that reading the lines
        /// again yields the same options.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="zone">The zone the times are written in.</param>
        /// <param name="culture">The culture the days are written in.</param>
        /// <returns>The lines.</returns>
        public static string Write(IEnumerable<PollOption> options, TimeZoneInfo zone, CultureInfo culture)
        {
            return string.Join("\n", (options ?? []).Select(x => Write(x, zone, culture)));
        }

        /// <summary>
        /// Writes one option as a line.
        /// </summary>
        /// <param name="option">The option.</param>
        /// <param name="zone">The zone the times are written in.</param>
        /// <param name="culture">The culture the days are written in.</param>
        /// <returns>The line.</returns>
        public static string Write(PollOption option, TimeZoneInfo zone, CultureInfo culture)
        {
            if (option?.Start is not DateTime start)
            {
                return option?.Text ?? string.Empty;
            }

            culture ??= CultureInfo.CurrentCulture;

            var pattern = culture.DateTimeFormat.ShortDatePattern;
            var localStart = PollTime.ToLocal(start, zone);
            string line;

            if (option.AllDay)
            {
                var lastDay = option.End is DateTime end && PollTime.ToLocal(end, zone) > localStart
                    ? PollTime.ToLocal(end, zone).AddTicks(-1).Date
                    : localStart.Date;

                line = lastDay <= localStart.Date
                    ? localStart.ToString(pattern, culture)
                    : $"{localStart.ToString(pattern, culture)} - {lastDay.ToString(pattern, culture)}";
            }
            else if (option.End is not DateTime end || end == start)
            {
                line = $"{localStart.ToString(pattern, culture)} {localStart.ToString("HH:mm", CultureInfo.InvariantCulture)}";
            }
            else
            {
                var localEnd = PollTime.ToLocal(end, zone);

                line = localEnd.Date == localStart.Date
                    ? $"{localStart.ToString(pattern, culture)} {localStart.ToString("HH:mm", CultureInfo.InvariantCulture)} - {localEnd.ToString("HH:mm", CultureInfo.InvariantCulture)}"
                    : $"{localStart.ToString(pattern, culture)} {localStart.ToString("HH:mm", CultureInfo.InvariantCulture)} - {localEnd.ToString(pattern, culture)} {localEnd.ToString("HH:mm", CultureInfo.InvariantCulture)}";
            }

            return string.IsNullOrWhiteSpace(option.Text) ? line : $"{line} | {option.Text}";
        }

        /// <summary>
        /// Gives the options read from a dialog the ids of the stored options they equal, so
        /// that an option left as it was keeps its votes; a changed option is a new one.
        /// </summary>
        /// <param name="read">The options read.</param>
        /// <param name="stored">The options stored.</param>
        /// <returns>The options read, with the ids of their stored equals.</returns>
        public static IReadOnlyList<PollOption> Match(IEnumerable<PollOption> read, IEnumerable<PollOption> stored)
        {
            var free = (stored ?? []).ToList();
            var result = new List<PollOption>();

            foreach (var option in read ?? [])
            {
                var match = free.FirstOrDefault(x => Same(x, option));

                if (match is not null)
                {
                    option.Id = match.Id;
                    free.Remove(match);
                }

                result.Add(option);
            }

            return result;
        }

        /// <summary>
        /// Determines whether two options are the same: the same date and text.
        /// </summary>
        /// <param name="a">The one option.</param>
        /// <param name="b">The other.</param>
        /// <returns><see langword="true"/> when they are the same.</returns>
        public static bool Same(PollOption a, PollOption b)
        {
            return a.Start == b.Start
                && a.End == b.End
                && a.AllDay == b.AllDay
                && string.Equals((a.Text ?? string.Empty).Trim(), (b.Text ?? string.Empty).Trim(), StringComparison.CurrentCultureIgnoreCase);
        }

        /// <summary>
        /// Reads a choice.
        /// </summary>
        /// <param name="line">The line.</param>
        /// <returns>The option, or <see langword="null"/> for a line too long.</returns>
        private static PollOption ParseChoice(string line)
        {
            return line.Length > MaxText ? null : new PollOption { Text = line };
        }

        /// <summary>
        /// Reads a proposed date.
        /// </summary>
        /// <param name="line">The line.</param>
        /// <param name="zone">The zone.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="duration">The default duration.</param>
        /// <returns>The option, or <see langword="null"/> for a line that is no date.</returns>
        private static PollOption ParseDate(string line, TimeZoneInfo zone, CultureInfo culture, TimeSpan duration)
        {
            string remark = null;
            var bar = line.IndexOf('|');

            if (bar >= 0)
            {
                remark = line[(bar + 1)..].Trim();
                line = line[..bar].Trim();

                if (remark.Length == 0)
                {
                    remark = null;
                }
                else if (remark.Length > MaxText)
                {
                    return null;
                }
            }

            // dashes of any kind separate the start from the end; one between two times may stand
            // without blanks (14:00-15:30), a date written with dashes keeps its own
            line = line.Replace('–', '-').Replace('—', '-');
            line = TimeSpanPattern().Replace(line, "$1 - $2");

            var parts = SeparatorPattern().Split(line, 2);
            var from = Tokens(parts[0]);
            var to = parts.Length > 1 ? Tokens(parts[1]) : [];

            if (from.Length is < 1 or > 2 || to.Length > 2 || (parts.Length > 1 && to.Length == 0))
            {
                return null;
            }

            if (!PollTime.TryParseDay(from[0], culture, out var startDay))
            {
                return null;
            }

            TimeSpan startTime = default;
            var timed = from.Length == 2;

            if (timed && !PollTime.TryParseTime(from[1], out startTime))
            {
                return null;
            }

            if (!timed)
            {
                // whole days: the end is the midnight after the last day
                var lastDay = startDay;

                if (to.Length == 2 || (to.Length == 1 && !PollTime.TryParseDay(to[0], culture, out lastDay)))
                {
                    return null;
                }

                if (lastDay < startDay)
                {
                    return null;
                }

                return new PollOption
                {
                    Start = PollTime.ToUtc(startDay, zone),
                    End = PollTime.ToUtc(lastDay.AddDays(1), zone),
                    AllDay = true,
                    Text = remark
                };
            }

            var start = startDay + startTime;
            DateTime? end = duration > TimeSpan.Zero ? start + duration : null;

            if (to.Length == 1)
            {
                if (!PollTime.TryParseTime(to[0], out var endTime))
                {
                    return null;
                }

                end = startDay + endTime;
            }
            else if (to.Length == 2)
            {
                if (!PollTime.TryParseDay(to[0], culture, out var endDay) || !PollTime.TryParseTime(to[1], out var endTime))
                {
                    return null;
                }

                end = endDay + endTime;
            }

            if (end is DateTime e && e < start)
            {
                return null;
            }

            return new PollOption
            {
                Start = PollTime.ToUtc(start, zone),
                End = end is DateTime localEnd ? PollTime.ToUtc(localEnd, zone) : null,
                AllDay = false,
                Text = remark
            };
        }

        /// <summary>
        /// Splits a part of a line at its blanks.
        /// </summary>
        /// <param name="part">The part.</param>
        /// <returns>The tokens.</returns>
        private static string[] Tokens(string part)
        {
            return (part ?? string.Empty).Split((char[])null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        [GeneratedRegex(@"([0-9]{1,2}[:.][0-9]{2})\s*-\s*([0-9]{1,2}[:.][0-9]{2})")]
        private static partial Regex TimeSpanPattern();

        [GeneratedRegex(@"\s+-\s+")]
        private static partial Regex SeparatorPattern();
    }
}
