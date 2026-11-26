using KleeneStar.Core;
using KleeneStar.Model.Entities;
using KleeneStar.Poll.WebPoll;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;
using VoteApi = global::KleeneStar.Poll.WWW.Api._1_.Poll._objectkey_.Vote;
using CloseApi = global::KleeneStar.Poll.WWW.Api._1_.Poll._objectkey_.Close;

namespace KleeneStar.Poll.WebFragment.Poll
{
    /// <summary>
    /// The dialog creating a new poll in a workspace
    /// (<see cref="global::KleeneStar.Poll.WWW.Polls._workspacekey_.Add"/>). Opened with
    /// <c>?type=</c>, it starts on that poll type; the type travels as the form's item id.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Poll.WWW.Polls._workspacekey_.Add>]
    [Cache]
    public sealed class PollAddFormFragment : FragmentControlDataFormAdd
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public PollAddFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Poll.WWW.Api._1_.Polls._workspacekey_.Index>();
            ItemId = renderContext => renderContext.Request.GetParameter("type")?.Value;
            Submit.Text = _ => "kleenestar.poll:poll.add.submit";
            Submit.Icon = _ => new IconPlus();
            SuccessAction = _ => TypeRestFormSuccess.FollowUri;
        }

        /// <summary>
        /// Renders the dialog with the inputs bound to the workspace.
        /// </summary>
        /// <param name="renderContext">The context in which the form is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The dialog, or <see langword="null"/> without a class to create in.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            var workspace = PollRequest.Workspace(renderContext.Request);

            if (workspace is null || !PollRequest.Classes(workspace).Any(x => PollAuthorization.MayCreate(x, renderContext.Request)))
            {
                return null;
            }

            return base.Render(renderContext, visualTree, PollFormItems.Build(workspace, null));
        }
    }

    /// <summary>
    /// The dialog changing the question, the options and the settings of a poll
    /// (<see cref="global::KleeneStar.Poll.WWW.Poll._objectkey_.Edit"/>). Its item id is the
    /// poll's, so the detail page reloads once it saved (<c>kleenestar.core.object.refresh.js</c>).
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Poll.WWW.Poll._objectkey_.Edit>]
    [Cache]
    public sealed class PollEditFormFragment : FragmentControlDataFormEdit
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public PollEditFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Poll.WWW.Api._1_.Poll._objectkey_.Index>();
            ItemId = renderContext => PollRequest.Poll(renderContext.Request)?.Id.ToString();
            Submit.Text = _ => "kleenestar.poll:poll.edit.submit";
            Submit.Icon = _ => new IconPenToSquare();
        }

        /// <summary>
        /// Renders the dialog.
        /// </summary>
        /// <param name="renderContext">The context in which the form is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The dialog, or <see langword="null"/> without a poll.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            var poll = PollRequest.Poll(renderContext.Request);

            if (poll is null)
            {
                return null;
            }

            var workspace = poll.Workspace ?? CoreHub.WorkspaceManager.GetWorkspace(poll.WorkspaceId);

            return base.Render(renderContext, visualTree, PollFormItems.Build(workspace, poll));
        }
    }

    /// <summary>
    /// The dialog in which an account votes
    /// (<see cref="global::KleeneStar.Poll.WWW.Poll._objectkey_.Vote"/>): every proposed date of
    /// a scheduling poll is answered with yes, if need be or no; a decision offers its options as
    /// radio buttons, or as check boxes where several may be chosen.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Poll.WWW.Poll._objectkey_.Vote>]
    [Cache]
    public sealed class PollVoteFormFragment : FragmentControlDataFormAdd
    {
        /// <summary>
        /// The colours of the answers: green, amber, red.
        /// </summary>
        private static readonly Dictionary<PollAnswer, string> _colors = new()
        {
            [PollAnswer.Yes] = "#198754",
            [PollAnswer.Maybe] = "#fd7e14",
            [PollAnswer.No] = "#dc3545"
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public PollVoteFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<VoteApi>();
            ItemId = renderContext => PollRequest.Poll(renderContext.Request)?.Id.ToString();
            Submit.Text = _ => "kleenestar.poll:vote.submit";
            Submit.Icon = _ => new IconCheckToSlot();
            SuccessAction = _ => TypeRestFormSuccess.FollowUri;
        }

        /// <summary>
        /// Renders the dialog with one input per option.
        /// </summary>
        /// <param name="renderContext">The context in which the form is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The dialog, or <see langword="null"/> for a poll without options.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            var request = renderContext.Request;
            var entity = PollRequest.Poll(request);
            var poll = entity is null ? null : PollHub.PollManager.GetPoll(entity.Id);
            var options = entity is null ? [] : PollHub.PollManager.GetOptions(entity.Id);

            if (poll is null || options.Count == 0)
            {
                return null;
            }

            var type = PollHub.PollTypeManager.Resolve(poll.Type);
            var zone = PollRequest.Zone(request);
            var culture = PollRequest.Culture(request);
            var items = new List<ControlFormItem>
            {
                new ControlFormItemStaticText("poll-vote-text")
                {
                    Text = _ => type.Graded
                        ? "kleenestar.poll:vote.help.graded"
                        : poll.MultipleChoice ? "kleenestar.poll:vote.help.multiple" : "kleenestar.poll:vote.help.single"
                }
            };

            if (type.Graded)
            {
                var answers = poll.AllowMaybe
                    ? new[] { PollAnswer.Yes, PollAnswer.Maybe, PollAnswer.No }
                    : new[] { PollAnswer.Yes, PollAnswer.No };

                foreach (var option in options)
                {
                    var label = PollTime.Describe(option, zone, culture);
                    var choice = new ControlFormItemInputChoice
                    {
                        Name = _ => VoteApi.AnswerPrefix + option.Id.ToString("N"),
                        Label = _ => label
                    };

                    foreach (var answer in answers)
                    {
                        choice.Add(new ControlFormItemInputChoiceItem
                        {
                            Text = _ => "kleenestar.poll:answer." + answer.ToString().ToLowerInvariant(),
                            Value = _ => answer.ToString().ToLowerInvariant(),
                            Description = _ => "kleenestar.poll:answer." + answer.ToString().ToLowerInvariant() + ".description",
                            Color = _ => new PropertyColorTile(_colors[answer])
                        });
                    }

                    items.Add(choice);
                }
            }
            else if (poll.MultipleChoice)
            {
                foreach (var option in options)
                {
                    var label = PollTime.Describe(option, zone, culture);

                    items.Add(new ControlFormItemInputCheck
                    {
                        Name = _ => VoteApi.ChoicePrefix + option.Id.ToString("N"),
                        Label = _ => label
                    });
                }
            }
            else
            {
                // a radio group is one control per option sharing the name
                var group = new ControlFormItemGroupVertical();

                foreach (var option in options)
                {
                    var label = PollTime.Describe(option, zone, culture);

                    group.Add(new ControlFormItemInputRadio
                    {
                        Name = _ => VoteApi.ChoiceField,
                        Option = _ => option.Id.ToString("N"),
                        Description = _ => label
                    });
                }

                items.Add(group);
            }

            return base.Render(renderContext, visualTree, items);
        }
    }

    /// <summary>
    /// The dialog closing a poll (<see cref="global::KleeneStar.Poll.WWW.Poll._objectkey_.Close"/>):
    /// the option it settled on is chosen - the leading one is proposed - or none.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Poll.WWW.Poll._objectkey_.Close>]
    [Cache]
    public sealed class PollCloseFormFragment : FragmentControlDataFormAdd
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public PollCloseFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<CloseApi>();
            ItemId = renderContext => PollRequest.Poll(renderContext.Request)?.Id.ToString();
            Submit.Text = _ => "kleenestar.poll:close.submit";
            Submit.Icon = _ => new IconFlagCheckered();
            SuccessAction = _ => TypeRestFormSuccess.FollowUri;
        }

        /// <summary>
        /// Renders the dialog with the options and their counts.
        /// </summary>
        /// <param name="renderContext">The context in which the form is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The dialog, or <see langword="null"/> without a poll.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            var request = renderContext.Request;
            var entity = PollRequest.Poll(request);
            var result = entity is null ? null : PollHub.PollManager.GetResult(entity.Id);

            if (result is null)
            {
                return null;
            }

            var zone = PollRequest.Zone(request);
            var culture = PollRequest.Culture(request);
            var selection = new ControlFormItemInputSelection
            {
                Name = _ => CloseApi.ChosenField,
                Label = _ => result.Type.Dated ? "kleenestar.poll:close.chosen.date" : "kleenestar.poll:close.chosen.option",
                Help = _ => "kleenestar.poll:close.chosen.help",
                StickySelection = _ => true
            };

            selection.Add(new ControlFormItemInputSelectionItem(CloseApi.NoneValue)
            {
                Text = _ => "kleenestar.poll:close.chosen.none"
            });

            foreach (var option in result.Options)
            {
                var text = PollTime.Describe(option.Option, zone, culture) + " – " + PollCards.Counts(option, result.Type, request);

                selection.Add(new ControlFormItemInputSelectionItem(option.Option.Id.ToString("N"))
                {
                    Text = _ => text
                });
            }

            return base.Render(renderContext, visualTree,
            [
                new ControlFormItemStaticText("poll-close-text")
                {
                    Text = _ => I18N.Translate(request, "kleenestar.poll:close.description", result.Voters.Count)
                },
                selection
            ]);
        }
    }

    /// <summary>
    /// The dialog opening a closed poll again
    /// (<see cref="global::KleeneStar.Poll.WWW.Poll._objectkey_.Reopen"/>). The endpoint names the
    /// poll in the prolog of the dialog.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Poll.WWW.Poll._objectkey_.Reopen>]
    [Cache]
    public sealed class PollReopenFormFragment : FragmentControlDataFormAdd
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public PollReopenFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Poll.WWW.Api._1_.Poll._objectkey_.Reopen>();
            ItemId = renderContext => PollRequest.Poll(renderContext.Request)?.Id.ToString();
            Submit.Text = _ => "kleenestar.poll:reopen.submit";
            Submit.Icon = _ => new IconRotateLeft();
            SuccessAction = _ => TypeRestFormSuccess.FollowUri;

            Add(new ControlFormItemStaticText("poll-reopen-text")
            {
                Text = _ => "kleenestar.poll:reopen.description"
            });
        }
    }
}
