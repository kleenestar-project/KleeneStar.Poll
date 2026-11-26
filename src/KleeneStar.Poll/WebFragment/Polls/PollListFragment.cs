using KleeneStar.Core;
using KleeneStar.Model.Entities;
using KleeneStar.Poll.WebPoll;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;
using ObjectEntity = KleeneStar.Model.Entities.Object;
using PollEntity = KleeneStar.Model.Entities.Poll;

namespace KleeneStar.Poll.WebFragment.Polls
{
    /// <summary>
    /// The content of the polls page of a workspace: the running polls (the next deadline
    /// first) and below them the closed ones (the last closed first, at most
    /// <see cref="ClosedLimit"/>), each with its type, turnout and deadline or result.
    /// </summary>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Poll.WWW.Polls._workspacekey_.Index>]
    [Order(0)]
    [Cache]
    public sealed class PollListFragment : FragmentControlPanel
    {
        /// <summary>
        /// The most closed polls listed.
        /// </summary>
        public const int ClosedLimit = 50;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public PollListFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Renders the two lists.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The lists, or <see langword="null"/> without a workspace.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var request = renderContext?.Request;
            var workspace = PollRequest.Workspace(request);

            if (workspace is null || !PollAuthorization.MayRead(workspace, request))
            {
                return null;
            }

            var workspaceId = workspace.Id;
            var objects = CoreHub.ObjectManager
                .GetObjects(new Query<ObjectEntity>().Where(x => x.WorkspaceId == workspaceId && x.Kind == ObjectKind.Poll))
                .Where(x => x.State == WorkspaceState.Active)
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .ToList();

            var manager = PollHub.PollManager;
            var polls = manager.GetPolls(objects.Select(x => x.Id));
            var voters = manager.GetVoterCounts(objects.Select(x => x.Id));
            var voted = manager.GetVotedBy(objects.Select(x => x.Id), PollRequest.Identity(request));
            var now = DateTime.UtcNow;

            var running = objects
                .Where(x => polls.TryGetValue(x.Id, out var poll) && manager.IsRunning(poll, now))
                .OrderBy(x => polls[x.Id].Deadline ?? DateTime.MaxValue)
                .ThenByDescending(x => x.Created)
                .ToList();

            var closed = objects
                .Where(x => !running.Contains(x))
                .OrderByDescending(x => polls.TryGetValue(x.Id, out var poll) ? poll.Closed ?? poll.Deadline ?? x.Updated : x.Updated)
                .Take(ClosedLimit)
                .ToList();

            var panel = new ControlPanel("poll-list");

            panel.Add(Section("poll-list-running", "kleenestar.poll:list.running", new IconPlay(), running, polls, voters, voted, request, closedList: false));
            panel.Add(Section("poll-list-closed", "kleenestar.poll:list.closed", new IconFlagCheckered(), closed, polls, voters, voted, request, closedList: true));

            return panel.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Builds one of the two lists.
        /// </summary>
        /// <param name="id">The id of the section.</param>
        /// <param name="header">The internationalization key of the header.</param>
        /// <param name="icon">The icon of the header.</param>
        /// <param name="objects">The polls listed.</param>
        /// <param name="polls">The settings of the polls.</param>
        /// <param name="voters">The numbers of voters.</param>
        /// <param name="voted">The polls the reader voted in.</param>
        /// <param name="request">The request.</param>
        /// <param name="closedList">Whether the list shows the closed polls.</param>
        /// <returns>The section.</returns>
        private static ControlSection Section
        (
            string id,
            string header,
            WebExpress.WebCore.WebIcon.IIcon icon,
            IReadOnlyList<ObjectEntity> objects,
            IReadOnlyDictionary<Guid, PollEntity> polls,
            IReadOnlyDictionary<Guid, int> voters,
            IReadOnlySet<Guid> voted,
            WebExpress.WebCore.WebMessage.IRequest request,
            bool closedList
        )
        {
            var culture = PollRequest.Culture(request);
            var zone = PollRequest.Zone(request);

            var section = new ControlSection(id)
            {
                Header = _ => header,
                HeaderIcon = _ => icon,
                Layout = _ => TypeLayoutSection.Rule,
                Badge = _ => objects.Count.ToString(culture)
            };

            if (objects.Count == 0)
            {
                section.Add(new ControlText(id + "-none")
                {
                    Text = _ => I18N.Translate(request, closedList ? "kleenestar.poll:list.closed.none" : "kleenestar.poll:list.running.none"),
                    Format = _ => TypeFormatText.Small,
                    TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
                });

                return section;
            }

            var list = new ControlList(id + "-list");

            foreach (var entity in objects)
            {
                polls.TryGetValue(entity.Id, out var poll);

                var type = PollHub.PollTypeManager.Resolve(poll?.Type);
                var count = voters.GetValueOrDefault(entity.Id);
                var parts = new List<string>
                {
                    I18N.Translate(request, type.Label),
                    I18N.Translate(request, count == 1 ? "kleenestar.poll:list.voter" : "kleenestar.poll:list.voters", count)
                };

                if (!closedList && poll?.Deadline is DateTime deadline)
                {
                    parts.Add(I18N.Translate(request, "kleenestar.poll:running.until", PollTime.ToLocal(deadline, zone).ToString("g", culture)));
                }

                if (closedList && poll?.ChosenOptionId is Guid chosenId)
                {
                    var chosen = PollHub.PollManager.GetOptions(entity.Id).FirstOrDefault(x => x.Id == chosenId);

                    if (chosen is not null)
                    {
                        parts.Add(I18N.Translate(request, "kleenestar.poll:list.result", PollTime.Describe(chosen, zone, culture)));
                    }
                }

                if (voted.Contains(entity.Id))
                {
                    parts.Add(I18N.Translate(request, "kleenestar.poll:running.voted"));
                }

                var key = entity.Id.ToString("N");
                var item = new ControlListItem(id + "-" + key)
                {
                    Icon = _ => voted.Contains(entity.Id) && !closedList ? new IconCheck() : type.Icon,
                    Color = _ => new PropertyColorText(closedList ? TypeColorText.Secondary : TypeColorText.Default)
                };

                item.Add(new ControlLink(id + "-link-" + key)
                {
                    Text = _ => string.IsNullOrWhiteSpace(entity.Summary) ? entity.Key : $"{entity.Summary} ({entity.Key})",
                    Uri = _ => CoreHub.ObjectKindManager.ResolveDetailUriFrozen(entity)
                });

                item.Add(new ControlText(id + "-text-" + key)
                {
                    Text = _ => string.Join(" · ", parts),
                    Format = _ => TypeFormatText.Small,
                    TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
                });

                list.Add(item);
            }

            section.Add(list);

            return section;
        }
    }
}
