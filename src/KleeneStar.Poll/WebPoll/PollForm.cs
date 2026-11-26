using KleeneStar.Model.Entities;
using KleeneStar.Poll.WebRestApi;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebMessage;
using ObjectEntity = KleeneStar.Model.Entities.Object;
using PollEntity = KleeneStar.Model.Entities.Poll;

namespace KleeneStar.Poll.WebPoll
{
    /// <summary>
    /// What the poll dialog submits, read and checked.
    /// </summary>
    public sealed class PollDraft
    {
        /// <summary>
        /// Gets or sets the question.
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        /// Gets or sets the description (an editor document), or <see langword="null"/> when the
        /// dialog did not send one.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the poll type.
        /// </summary>
        public IPollType Type { get; set; }

        /// <summary>
        /// Gets or sets the options, in their order.
        /// </summary>
        public IReadOnlyList<PollOption> Options { get; set; } = [];

        /// <summary>
        /// Gets or sets whether several options may be chosen.
        /// </summary>
        public bool MultipleChoice { get; set; }

        /// <summary>
        /// Gets or sets whether "if need be" may be answered.
        /// </summary>
        public bool AllowMaybe { get; set; }

        /// <summary>
        /// Gets or sets whether the results show counts only.
        /// </summary>
        public bool Anonymous { get; set; }

        /// <summary>
        /// Gets or sets whether the results stay hidden until the poll is closed.
        /// </summary>
        public bool HideResults { get; set; }

        /// <summary>
        /// Gets or sets when voting ends (UTC), or <see langword="null"/>.
        /// </summary>
        public DateTime? Deadline { get; set; }

        /// <summary>
        /// Gets or sets the zone the dates were entered in.
        /// </summary>
        public TimeZoneInfo Zone { get; set; }
    }

    /// <summary>
    /// The inputs of the poll dialog, shared by the dialog creating a poll and the one changing
    /// it: the values a dialog starts from and the reading of what it submits.
    /// </summary>
    public static class PollForm
    {
        /// <summary>Input name of the question.</summary>
        public const string Summary = "Summary";

        /// <summary>Input name of the description.</summary>
        public const string Description = "Description";

        /// <summary>Input name of the class of a new poll.</summary>
        public const string ClassId = "Class";

        /// <summary>Input name of the poll type.</summary>
        public const string Type = "Type";

        /// <summary>Input name of the options, one per line.</summary>
        public const string Options = "Options";

        /// <summary>Input name of the multiple choice switch.</summary>
        public const string MultipleChoice = "MultipleChoice";

        /// <summary>Input name of the "if need be" switch.</summary>
        public const string AllowMaybe = "AllowMaybe";

        /// <summary>Input name of the anonymity switch.</summary>
        public const string Anonymous = "Anonymous";

        /// <summary>Input name of the switch hiding the results.</summary>
        public const string HideResults = "HideResults";

        /// <summary>Input name of the day of the deadline.</summary>
        public const string DeadlineDate = "DeadlineDate";

        /// <summary>Input name of the time of the deadline.</summary>
        public const string DeadlineTime = "DeadlineTime";

        /// <summary>The pattern a time of day is entered in.</summary>
        public const string TimePattern = "([01]?[0-9]|2[0-3])[:.][0-5][0-9]";

        /// <summary>
        /// Answers the values the dialog starts from.
        /// </summary>
        /// <param name="entity">The poll object, or <see langword="null"/> for a new poll.</param>
        /// <param name="poll">The settings, or <see langword="null"/>.</param>
        /// <param name="options">The options.</param>
        /// <param name="zone">The zone of the reader.</param>
        /// <param name="culture">The culture of the reader.</param>
        /// <returns>The values, keyed by input name.</returns>
        public static Dictionary<string, object> Values(ObjectEntity entity, PollEntity poll, IEnumerable<PollOption> options, TimeZoneInfo zone, CultureInfo culture)
        {
            culture ??= CultureInfo.CurrentCulture;

            var deadline = poll?.Deadline is DateTime d ? PollTime.ToLocal(d, zone) : (DateTime?)null;

            return new Dictionary<string, object>
            {
                [Summary] = entity?.Summary ?? string.Empty,
                [Description] = entity?.Description ?? string.Empty,
                [Type] = poll?.Type ?? SchedulePollType.TypeKey,
                [Options] = PollOptionText.Write(options, zone, culture),
                [MultipleChoice] = poll?.MultipleChoice ?? false,
                [AllowMaybe] = poll?.AllowMaybe ?? true,
                [Anonymous] = poll?.Anonymous ?? false,
                [HideResults] = poll?.HideResults ?? false,
                [DeadlineDate] = deadline?.ToString(culture.DateTimeFormat.ShortDatePattern, culture) ?? string.Empty,
                [DeadlineTime] = deadline?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? string.Empty
            };
        }

        /// <summary>
        /// Reads and checks what the dialog submitted.
        /// </summary>
        /// <param name="payload">The payload.</param>
        /// <param name="request">The request, for the language, the zone and the culture.</param>
        /// <param name="stored">The options stored so far, whose ids unchanged options keep;
        /// empty for a new poll.</param>
        /// <param name="storedDeadline">The deadline stored so far, which may lie in the past;
        /// <see langword="null"/> for a new poll.</param>
        /// <param name="errors">Receives the translated messages, keyed by input name.</param>
        /// <returns>The draft; only to be written when <paramref name="errors"/> stays empty.</returns>
        public static PollDraft Read(FormPayload payload, IRequest request, IReadOnlyList<PollOption> stored, DateTime? storedDeadline, IDictionary<string, string> errors)
        {
            var culture = PollRequest.Culture(request);
            var zone = PollRequest.Zone(request);

            var draft = new PollDraft
            {
                Summary = payload.Text(Summary),
                Description = payload.Has(Description) ? payload.Raw(Description) ?? string.Empty : null,
                Type = PollHub.PollTypeManager.GetType(payload.Text(Type)),
                MultipleChoice = payload.Flag(MultipleChoice),
                AllowMaybe = payload.Flag(AllowMaybe),
                Anonymous = payload.Flag(Anonymous),
                HideResults = payload.Flag(HideResults),
                Zone = zone
            };

            if (string.IsNullOrWhiteSpace(draft.Summary))
            {
                errors[Summary] = I18N.Translate(request, "kleenestar.poll:poll.summary.required");
            }

            if (draft.Type is null)
            {
                errors[Type] = I18N.Translate(request, "kleenestar.poll:poll.type.required");

                return draft;
            }

            var unread = new List<string>();
            var options = PollOptionText.Parse
            (
                payload.Raw(Options),
                draft.Type.Dated,
                zone,
                culture,
                TimeSpan.FromMinutes(PollHub.Settings.DefaultDuration),
                unread
            );

            if (unread.Count > 0)
            {
                errors[Options] = I18N.Translate(request, draft.Type.Dated ? "kleenestar.poll:poll.options.invalid.date" : "kleenestar.poll:poll.options.invalid.text", string.Join("; ", unread.Take(5)));
            }
            else if (options.Count > PollOptionText.MaxOptions)
            {
                errors[Options] = I18N.Translate(request, "kleenestar.poll:poll.options.many", PollOptionText.MaxOptions);
            }
            else if (options.Count < (draft.Type.Graded ? 1 : 2))
            {
                errors[Options] = I18N.Translate(request, draft.Type.Graded ? "kleenestar.poll:poll.options.one" : "kleenestar.poll:poll.options.two");
            }

            draft.Options = PollOptionText.Match(options, stored);

            var day = payload.Text(DeadlineDate);
            var time = payload.Text(DeadlineTime);

            if (day is not null)
            {
                var clock = new TimeSpan(23, 59, 0);

                if (!PollTime.TryParseDay(day, culture, out var deadlineDay) || (time is not null && !PollTime.TryParseTime(time, out clock)))
                {
                    errors[DeadlineDate] = I18N.Translate(request, "kleenestar.poll:poll.deadline.invalid");
                }
                else
                {
                    draft.Deadline = PollTime.ToUtc(deadlineDay + clock, zone);

                    // a deadline kept as it was may have passed; a new one must lie ahead
                    if (draft.Deadline <= DateTime.UtcNow && !SameMinute(draft.Deadline, storedDeadline))
                    {
                        errors[DeadlineDate] = I18N.Translate(request, "kleenestar.poll:poll.deadline.past");
                    }
                }
            }
            else if (time is not null)
            {
                errors[DeadlineDate] = I18N.Translate(request, "kleenestar.poll:poll.deadline.day");
            }

            return draft;
        }

        /// <summary>
        /// Lays a draft over the settings of a poll.
        /// </summary>
        /// <param name="draft">The draft.</param>
        /// <param name="poll">The settings.</param>
        public static void Apply(PollDraft draft, PollEntity poll)
        {
            poll.Type = draft.Type.Key;
            poll.MultipleChoice = !draft.Type.Graded && draft.MultipleChoice;
            poll.AllowMaybe = draft.Type.Graded && draft.AllowMaybe;
            poll.Anonymous = draft.Anonymous;
            poll.HideResults = draft.HideResults;
            poll.Deadline = draft.Deadline;
            poll.TimeZone = draft.Zone?.Id ?? poll.TimeZone;
        }

        /// <summary>
        /// Determines whether two instants fall into the same minute, as the dialog writes them.
        /// </summary>
        /// <param name="a">The one instant.</param>
        /// <param name="b">The other.</param>
        /// <returns><see langword="true"/> when they do.</returns>
        private static bool SameMinute(DateTime? a, DateTime? b)
        {
            return a is DateTime x && b is DateTime y
                && Math.Abs((x - y).TotalSeconds) < 60;
        }
    }
}
