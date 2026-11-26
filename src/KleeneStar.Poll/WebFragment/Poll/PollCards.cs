using KleeneStar.Core;
using KleeneStar.Core.WebParameter;
using KleeneStar.Poll.WebPoll;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Poll.WebFragment.Poll
{
    /// <summary>
    /// What the cards of the poll page share: the counts of an option in words, the names of
    /// voters and the buttons opening the dialogs.
    /// </summary>
    public static class PollCards
    {
        /// <summary>
        /// Writes the counts of an option: <c>3 yes · 1 if need be · 2 no</c> for a graded poll,
        /// <c>3 votes</c> for a decision.
        /// </summary>
        /// <param name="option">The count of the option.</param>
        /// <param name="type">The type of the poll.</param>
        /// <param name="request">The request, for the language.</param>
        /// <returns>The text.</returns>
        public static string Counts(PollOptionResult option, IPollType type, IRequest request)
        {
            if (!type.Graded)
            {
                return I18N.Translate(request, option.Yes.Count == 1 ? "kleenestar.poll:result.vote" : "kleenestar.poll:result.votes", option.Yes.Count);
            }

            var parts = new List<string>
            {
                I18N.Translate(request, "kleenestar.poll:result.yes", option.Yes.Count)
            };

            if (option.Maybe.Count > 0)
            {
                parts.Add(I18N.Translate(request, "kleenestar.poll:result.maybe", option.Maybe.Count));
            }

            parts.Add(I18N.Translate(request, "kleenestar.poll:result.no", option.No.Count));

            return string.Join(" · ", parts);
        }

        /// <summary>
        /// Writes the names of some accounts, the reader as "you".
        /// </summary>
        /// <param name="ids">The accounts.</param>
        /// <param name="identityId">The reader.</param>
        /// <param name="request">The request, for the language.</param>
        /// <returns>The names, separated by commas.</returns>
        public static string Names(IEnumerable<Guid> ids, Guid identityId, IRequest request)
        {
            return string.Join(", ", (ids ?? []).Select(id => id == identityId
                ? I18N.Translate(request, "kleenestar.poll:result.you")
                : CoreHub.IdentityManager.GetIdentity(id)?.Name ?? I18N.Translate(request, "kleenestar.poll:result.unknown")));
        }

        /// <summary>
        /// Builds a button opening one of the dialogs of a poll.
        /// </summary>
        /// <typeparam name="TPage">The page of the dialog.</typeparam>
        /// <param name="id">The id of the button.</param>
        /// <param name="poll">The poll.</param>
        /// <param name="text">The internationalization key of the text.</param>
        /// <param name="icon">The icon.</param>
        /// <param name="color">The colour.</param>
        /// <param name="size">The size of the dialog.</param>
        /// <returns>The button.</returns>
        public static ControlButtonLink Button<TPage>(string id, ObjectEntity poll, string text, IIcon icon, TypeColorButton color, TypeModalSize size = TypeModalSize.Default)
            where TPage : WebExpress.WebCore.WebEndpoint.IEndpoint
        {
            return new ControlButtonLink(id)
            {
                Text = _ => text,
                Icon = _ => icon,
                BackgroundColor = _ => new PropertyColorButton(color),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.Two, PropertySpacing.Space.Two, PropertySpacing.Space.None),
                PrimaryAction = _ => new ActionModal("modal-form",
                    CoreHub.GetUri<TPage>()?.BindParameters(new ObjectKeyParameter(poll.Key)),
                    size)
            };
        }

        /// <summary>
        /// Builds a small text.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="text">The text, translated.</param>
        /// <param name="secondary">Whether it is shown subdued.</param>
        /// <returns>The text.</returns>
        public static ControlText Text(string id, string text, bool secondary = false)
        {
            return new ControlText(id)
            {
                Text = _ => text,
                Format = _ => TypeFormatText.Small,
                TextColor = _ => new PropertyColorText(secondary ? TypeColorText.Secondary : TypeColorText.Default)
            };
        }
    }

    /// <summary>
    /// The result card of the poll page, above the description: whether the poll runs and until
    /// when, what it settled on, and per option the share of the voters it suits with the counts
    /// and - unless the poll is anonymous - the names. The leading options are marked; a poll
    /// hiding its results until it is closed shows the turnout only. Below, the buttons to vote,
    /// change, close or open the poll, as far as the reader may.
    /// </summary>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Poll.WWW.Poll._objectkey_.Index>]
    [Order(-1)]
    [Cache]
    public sealed class PollResultCardFragment : FragmentControlPanel
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public PollResultCardFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Renders the card.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The card, or <see langword="null"/>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var request = renderContext?.Request;
            var entity = PollRequest.Poll(request);
            var result = entity is null ? null : PollHub.PollManager.GetResult(entity.Id);

            if (result is null)
            {
                return null;
            }

            var poll = result.Poll;
            var now = DateTime.UtcNow;
            var running = PollHub.PollManager.IsRunning(poll, now);
            var zone = PollRequest.Zone(request);
            var culture = PollRequest.Culture(request);
            var identityId = PollRequest.Identity(request);
            var visible = PollAuthorization.MaySeeResults(entity, poll, request);
            var writable = PollAuthorization.MayWrite(entity, request);

            var section = new ControlSection("poll-result-section")
            {
                Header = _ => running ? "kleenestar.poll:result.header.running" : "kleenestar.poll:result.header.closed",
                HeaderIcon = _ => result.Type.Icon,
                Layout = _ => TypeLayoutSection.Rule,
                Badge = _ => I18N.Translate(request, result.Voters.Count == 1 ? "kleenestar.poll:list.voter" : "kleenestar.poll:list.voters", result.Voters.Count)
            };

            section.Add(PollCards.Text("poll-result-state", State(poll, running, zone, culture, request), secondary: true));

            if (!running && poll.ChosenOptionId is Guid chosenId && result.Options.FirstOrDefault(x => x.Option.Id == chosenId) is { } chosen)
            {
                section.Add(new ControlAlert("poll-result-chosen")
                {
                    Text = _ => I18N.Translate(request, result.Type.Dated ? "kleenestar.poll:result.chosen.date" : "kleenestar.poll:result.chosen.option", PollTime.Describe(chosen.Option, zone, culture)),
                    BackgroundColor = _ => new PropertyColorBackgroundAlert(TypeColorBackgroundAlert.Success),
                    Dismissibility = _ => TypeDismissibilityAlert.None
                });
            }

            if (result.Options.Count == 0)
            {
                section.Add(PollCards.Text("poll-result-empty", I18N.Translate(request, writable ? "kleenestar.poll:result.empty.writable" : "kleenestar.poll:result.empty"), secondary: true));
            }
            else if (!visible)
            {
                section.Add(PollCards.Text("poll-result-hidden", I18N.Translate(request, "kleenestar.poll:result.hidden"), secondary: true));
            }
            else
            {
                foreach (var option in result.Options)
                {
                    AddOption(section, option, result, identityId, zone, culture, request);
                }
            }

            if (result.Options.Count > 0)
            {
                section.Add(PollCards.Text("poll-result-mine", Mine(result, identityId, zone, culture, request)));
            }

            var actions = new ControlPanel("poll-result-actions");

            if (running && result.Options.Count > 0 && PollAuthorization.MayVote(entity, request))
            {
                var hasVoted = result.Voters.Contains(identityId);

                actions.Add(PollCards.Button<global::KleeneStar.Poll.WWW.Poll._objectkey_.Vote>("poll-result-vote", entity,
                    hasVoted ? "kleenestar.poll:vote.change" : "kleenestar.poll:vote.label", new IconCheckToSlot(), TypeColorButton.Primary, TypeModalSize.Large));
            }

            if (writable)
            {
                actions.Add(PollCards.Button<global::KleeneStar.Poll.WWW.Poll._objectkey_.Edit>("poll-result-edit", entity,
                    "kleenestar.poll:poll.edit.label", new IconPenToSquare(), TypeColorButton.Secondary, TypeModalSize.ExtraLarge));

                actions.Add(running
                    ? PollCards.Button<global::KleeneStar.Poll.WWW.Poll._objectkey_.Close>("poll-result-close", entity,
                        "kleenestar.poll:close.label", new IconFlagCheckered(), TypeColorButton.Success)
                    : PollCards.Button<global::KleeneStar.Poll.WWW.Poll._objectkey_.Reopen>("poll-result-reopen", entity,
                        "kleenestar.poll:reopen.label", new IconRotateLeft(), TypeColorButton.Warning));
            }

            section.Add(actions);

            return section.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Adds the lines of one option: its text, a bar of the share it suits and the counts.
        /// </summary>
        /// <param name="section">The card.</param>
        /// <param name="option">The count of the option.</param>
        /// <param name="result">The result.</param>
        /// <param name="identityId">The reader.</param>
        /// <param name="zone">The zone of the reader.</param>
        /// <param name="culture">The culture of the reader.</param>
        /// <param name="request">The request.</param>
        private static void AddOption(ControlSection section, PollOptionResult option, PollResult result, Guid identityId, TimeZoneInfo zone, CultureInfo culture, IRequest request)
        {
            var key = option.Option.Id.ToString("N");
            var chosen = result.Poll.ChosenOptionId == option.Option.Id;
            var mark = chosen ? "✔ " : option.Leading ? "★ " : string.Empty;

            section.Add(new ControlText("poll-option-" + key)
            {
                Text = _ => mark + PollTime.Describe(option.Option, zone, culture),
                Format = _ => option.Leading || chosen ? TypeFormatText.Bold : TypeFormatText.Span
            });

            section.Add(new ControlProgress("poll-option-bar-" + key)
            {
                Value = _ => (uint)Math.Clamp(option.Percent, 0, 100),
                Color = _ => new PropertyColorProgress(chosen || option.Leading ? TypeColorProgress.Success : TypeColorProgress.Primary),
                Text = _ => option.Percent.ToString(CultureInfo.InvariantCulture) + " %"
            });

            var counts = PollCards.Counts(option, result.Type, request);

            if (!result.Poll.Anonymous)
            {
                var names = new List<string>();

                if (option.Yes.Count > 0)
                {
                    names.Add((result.Type.Graded ? I18N.Translate(request, "kleenestar.poll:answer.yes") + ": " : string.Empty) + PollCards.Names(option.Yes, identityId, request));
                }

                if (option.Maybe.Count > 0)
                {
                    names.Add(I18N.Translate(request, "kleenestar.poll:answer.maybe") + ": " + PollCards.Names(option.Maybe, identityId, request));
                }

                if (option.No.Count > 0)
                {
                    names.Add(I18N.Translate(request, "kleenestar.poll:answer.no") + ": " + PollCards.Names(option.No, identityId, request));
                }

                if (names.Count > 0)
                {
                    counts += " – " + string.Join("; ", names);
                }
            }

            section.Add(PollCards.Text("poll-option-counts-" + key, counts, secondary: true));
        }

        /// <summary>
        /// Writes whether the poll runs, until when, or since when it is closed.
        /// </summary>
        /// <param name="poll">The settings of the poll.</param>
        /// <param name="running">Whether it runs.</param>
        /// <param name="zone">The zone of the reader.</param>
        /// <param name="culture">The culture of the reader.</param>
        /// <param name="request">The request.</param>
        /// <returns>The text.</returns>
        private static string State(Model.Entities.Poll poll, bool running, TimeZoneInfo zone, CultureInfo culture, IRequest request)
        {
            if (running)
            {
                return poll.Deadline is DateTime deadline
                    ? I18N.Translate(request, "kleenestar.poll:result.state.until", PollTime.ToLocal(deadline, zone).ToString("g", culture))
                    : I18N.Translate(request, "kleenestar.poll:result.state.open");
            }

            var closed = poll.Closed ?? poll.Deadline ?? DateTime.UtcNow;
            var by = poll.ClosedById is Guid id ? CoreHub.IdentityManager.GetIdentity(id)?.Name : null;

            return by is null
                ? I18N.Translate(request, "kleenestar.poll:result.state.closed", PollTime.ToLocal(closed, zone).ToString("g", culture))
                : I18N.Translate(request, "kleenestar.poll:result.state.closedby", PollTime.ToLocal(closed, zone).ToString("g", culture), by);
        }

        /// <summary>
        /// Writes what the reader voted.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="identityId">The reader.</param>
        /// <param name="zone">The zone of the reader.</param>
        /// <param name="culture">The culture of the reader.</param>
        /// <param name="request">The request.</param>
        /// <returns>The text.</returns>
        private static string Mine(PollResult result, Guid identityId, TimeZoneInfo zone, CultureInfo culture, IRequest request)
        {
            if (identityId == Guid.Empty || !result.Voters.Contains(identityId))
            {
                return I18N.Translate(request, "kleenestar.poll:result.mine.none");
            }

            if (!result.Type.Graded)
            {
                var chosen = result.Options.Where(x => x.Yes.Contains(identityId)).Select(x => PollTime.Describe(x.Option, zone, culture));

                return I18N.Translate(request, "kleenestar.poll:result.mine.chosen", string.Join("; ", chosen));
            }

            var yes = result.Options.Count(x => x.Yes.Contains(identityId));
            var maybe = result.Options.Count(x => x.Maybe.Contains(identityId));

            return I18N.Translate(request, "kleenestar.poll:result.mine.graded", yes, maybe, result.Options.Count);
        }
    }

    /// <summary>
    /// The settings card of the poll page: the type, the deadline, and how the poll is answered
    /// and shown.
    /// </summary>
    [Section<SectionPropertyPrimary>]
    [Scope<global::KleeneStar.Poll.WWW.Poll._objectkey_.Index>]
    [Order(-2)]
    [Cache]
    public sealed class PollSettingsCardFragment : FragmentControlPanel
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public PollSettingsCardFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Renders the card.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The card, or <see langword="null"/>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var request = renderContext?.Request;
            var entity = PollRequest.Poll(request);
            var poll = entity is null ? null : PollHub.PollManager.GetPoll(entity.Id);

            if (poll is null)
            {
                return null;
            }

            var type = PollHub.PollTypeManager.Resolve(poll.Type);
            var zone = PollRequest.Zone(request);
            var culture = PollRequest.Culture(request);

            var section = new ControlSection("poll-settings-section")
            {
                Header = _ => "kleenestar.poll:settings.header",
                HeaderIcon = _ => new IconGear(),
                Layout = _ => TypeLayoutSection.Rule
            };

            var list = new ControlList("poll-settings-list");

            list.Add(Item("type", type.Icon, I18N.Translate(request, "kleenestar.poll:settings.type"), I18N.Translate(request, type.Label)));
            list.Add(Item("deadline", new IconHourglassHalf(), I18N.Translate(request, "kleenestar.poll:settings.deadline"),
                poll.Deadline is DateTime deadline
                    ? PollTime.ToLocal(deadline, zone).ToString("g", culture)
                    : I18N.Translate(request, "kleenestar.poll:settings.deadline.none")));

            if (type.Graded)
            {
                list.Add(Item("maybe", new IconQuestion(), I18N.Translate(request, "kleenestar.poll:settings.maybe"), YesNo(poll.AllowMaybe, request)));
            }
            else
            {
                list.Add(Item("multiple", new IconListCheck(), I18N.Translate(request, "kleenestar.poll:settings.multiple"), YesNo(poll.MultipleChoice, request)));
            }

            list.Add(Item("anonymous", new IconUserSecret(), I18N.Translate(request, "kleenestar.poll:settings.anonymous"), YesNo(poll.Anonymous, request)));
            list.Add(Item("hidden", new IconLock(), I18N.Translate(request, "kleenestar.poll:settings.hidden"), YesNo(poll.HideResults, request)));

            if (type.Dated && !string.IsNullOrWhiteSpace(poll.TimeZone) && !string.Equals(PollTime.Zone(poll.TimeZone).Id, zone.Id, StringComparison.OrdinalIgnoreCase))
            {
                list.Add(Item("zone", new IconClock(), I18N.Translate(request, "kleenestar.poll:settings.zone"), zone.Id));
            }

            section.Add(list);

            return section.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Builds a line of the card.
        /// </summary>
        /// <param name="id">The id of the line.</param>
        /// <param name="icon">The icon.</param>
        /// <param name="label">The label, translated.</param>
        /// <param name="value">The value.</param>
        /// <returns>The line.</returns>
        private static ControlListItem Item(string id, IIcon icon, string label, string value)
        {
            // the list draws the text of an item only, so the label leads it
            return new ControlListItem("poll-settings-" + id)
            {
                Icon = _ => icon,
                Text = _ => $"{label}: {value}"
            };
        }

        /// <summary>
        /// Writes a switch as yes or no.
        /// </summary>
        /// <param name="value">The switch.</param>
        /// <param name="request">The request.</param>
        /// <returns>The text.</returns>
        private static string YesNo(bool value, IRequest request)
        {
            return I18N.Translate(request, value ? "kleenestar.poll:settings.on" : "kleenestar.poll:settings.off");
        }
    }

    /// <summary>
    /// The headline button of the poll page opening the vote dialog, for a reader who may vote
    /// while the poll runs.
    /// </summary>
    [Section<SectionHeadlinePrimary>]
    [Scope<global::KleeneStar.Poll.WWW.Poll._objectkey_.Index>]
    [Order(-1)]
    [Cache]
    public sealed class PollVoteButtonFragment : FragmentControlButtonLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public PollVoteButtonFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Text = _ => "kleenestar.poll:vote.label";
            Icon = _ => new IconCheckToSlot();
            Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two);
            BackgroundColor = _ => new PropertyColorButton(TypeColorButton.Primary);
            PrimaryAction = renderContext => new ActionModal
            (
                "modal-form",
                CoreHub.GetUri<global::KleeneStar.Poll.WWW.Poll._objectkey_.Vote>()?
                    .BindParameters(renderContext.Request),
                TypeModalSize.Large
            );
        }

        /// <summary>
        /// Renders the button, or nothing for a reader who may not vote or a poll that is over.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The button, or <see langword="null"/>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var request = renderContext?.Request;
            var entity = PollRequest.Poll(request);
            var poll = entity is null ? null : PollHub.PollManager.GetPoll(entity.Id);

            if (poll is null
                || !PollHub.PollManager.IsRunning(poll, DateTime.UtcNow)
                || PollHub.PollManager.GetOptions(entity.Id).Count == 0
                || !PollAuthorization.MayVote(entity, request))
            {
                return null;
            }

            return base.Render(renderContext, visualTree);
        }
    }

    /// <summary>
    /// The headline button of the polls page opening the dialog that creates a poll. It is shown
    /// to whoever may create an object in one of the workspace's poll classes.
    /// </summary>
    [Section<SectionHeadlinePrimary>]
    [Scope<global::KleeneStar.Poll.WWW.Polls._workspacekey_.Index>]
    [Order(0)]
    [Cache]
    public sealed class PollAddButtonFragment : FragmentControlButtonLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public PollAddButtonFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Text = _ => "kleenestar.poll:poll.add.label";
            Icon = _ => new IconPlus();
            Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two);
            BackgroundColor = _ => new PropertyColorButton(TypeColorButton.Primary);
            PrimaryAction = renderContext => new ActionModal
            (
                "modal-form",
                CoreHub.GetUri<global::KleeneStar.Poll.WWW.Polls._workspacekey_.Add>()?
                    .BindParameters(renderContext.Request),
                TypeModalSize.ExtraLarge
            );
        }

        /// <summary>
        /// Renders the button, or nothing for a caller who may not create a poll here.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree used for rendering the fragment.</param>
        /// <returns>The button, or <see langword="null"/>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var workspace = PollRequest.Workspace(renderContext?.Request);

            if (!PollRequest.Classes(workspace).Any(x => PollAuthorization.MayCreate(x, renderContext?.Request)))
            {
                return null;
            }

            return base.Render(renderContext, visualTree);
        }
    }
}
