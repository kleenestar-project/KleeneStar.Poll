using KleeneStar.Core;
using KleeneStar.Core.WebPolicies;
using KleeneStar.Poll.WebPoll;
using System;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Poll.WebFragment.Polls
{
    /// <summary>
    /// The "running polls" section of the poll sidebar, below the kind links: the polls of the
    /// workspace that still take votes - by default the first 10
    /// (<see cref="PollSettings.RunningCount"/>), the next deadline first, then the newest. Each
    /// entry shows the question, the deadline as its badge, a check mark where the reader has
    /// voted already, and links to the poll.
    /// </summary>
    [Section<SectionSidebarPrimary>]
    [Scope<global::KleeneStar.Poll.WWW.Polls._workspacekey_.Index>]
    [Scope<global::KleeneStar.Poll.WWW.Poll._objectkey_.Index>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<WorkspaceViewPolicy>>]
    [Order(10)]
    [Cache]
    public sealed class PollSidebarRunningFragment : FragmentControlSidebarItemLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public PollSidebarRunningFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Renders the header followed by one entry per running poll, or a disabled entry saying
        /// there is none.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree used for rendering the fragment.</param>
        /// <returns>The entries, or <see langword="null"/> when the fragment is suppressed.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var request = renderContext?.Request;

            if (!FragmentContext.Conditions.Check(request))
            {
                return null;
            }

            var workspace = PollRequest.Workspace(request);

            if (workspace is null || !PollAuthorization.MayRead(workspace, request))
            {
                return null;
            }

            var identityId = PollRequest.Identity(request);
            var now = DateTime.UtcNow;
            var running = PollHub.PollManager.GetRunning(workspace, identityId, now, PollHub.Settings.RunningCount);
            var currentKey = PollRequest.AnyObject(request)?.Key;

            var header = new ControlSidebarItemHeader(Id + "-header")
            {
                Text = _ => "kleenestar.poll:running.label"
            };

            var nodes = new HtmlList(header.Render(renderContext, visualTree));

            if (running.Count == 0)
            {
                var empty = new ControlSidebarItemLink("poll-running-empty")
                {
                    Text = _ => "kleenestar.poll:running.none",
                    Active = _ => TypeActive.Disabled
                };

                nodes.Add(empty.Render(renderContext, visualTree));

                return nodes;
            }

            var zone = PollRequest.Zone(request);
            var culture = PollRequest.Culture(request);

            foreach (var entry in running)
            {
                var entity = entry.Object;
                var type = PollHub.PollTypeManager.Resolve(entry.Poll.Type);
                var title = string.IsNullOrWhiteSpace(entity.Summary) ? entity.Key : entity.Summary;
                var local = entry.Poll.Deadline is DateTime deadline ? PollTime.ToLocal(deadline, zone) : (DateTime?)null;

                var tooltip = string.Join(" · ", new[]
                {
                    I18N.Translate(request, type.Label),
                    local is null
                        ? I18N.Translate(request, "kleenestar.poll:running.open")
                        : I18N.Translate(request, "kleenestar.poll:running.until", local.Value.ToString("g", culture)),
                    entry.Voted ? I18N.Translate(request, "kleenestar.poll:running.voted") : null
                }.Where(x => !string.IsNullOrWhiteSpace(x)));

                var link = new ControlSidebarItemLink("poll-running-" + entity.Id.ToString("N"))
                {
                    Text = _ => title,
                    Tooltip = _ => tooltip,
                    // frozen so the sidebar's request re-bind cannot repoint every entry at the
                    // object the page shows
                    Uri = _ => CoreHub.ObjectKindManager.ResolveDetailUriFrozen(entity),
                    Icon = _ => entry.Voted ? new IconCheck() : type.Icon,
                    Badge = _ => local is null
                        ? null
                        : local.Value.ToString(local.Value.Date == PollTime.ToLocal(now, zone).Date ? "HH:mm" : "ddd HH:mm", culture),
                    BadgeColor = _ => new PropertyColorBackgroundBadge(
                        entry.Poll.Deadline is DateTime d && d - now < TimeSpan.FromDays(1) ? TypeColorBackgroundBadge.Warning : TypeColorBackgroundBadge.Secondary),
                    Active = _ => string.Equals(entity.Key, currentKey, StringComparison.OrdinalIgnoreCase)
                        ? TypeActive.Active
                        : TypeActive.None
                };

                nodes.Add(link.Render(renderContext, visualTree));
            }

            return nodes;
        }
    }
}
