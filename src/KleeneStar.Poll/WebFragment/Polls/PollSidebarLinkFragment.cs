using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebPolicies;
using KleeneStar.Poll.WebPoll;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;

namespace KleeneStar.Poll.WebFragment.Polls
{
    /// <summary>
    /// Sidebar link to the polls of the current workspace, below the calendar (order 3) and
    /// above the issues (order 5). Like the core's kind links it is rendered on every kind
    /// overview and object detail page - the core's ones by name, an add-on's through the scope
    /// interfaces - and only where the workspace has a poll class.
    /// </summary>
    [Section<SectionSidebarPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Documents._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blogs._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Issues._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Assets._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Edit>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Edit>]
    [Scope<global::KleeneStar.Core.WebScope.IScopeObjectKindOverview>]
    [Scope<global::KleeneStar.Core.WebScope.IScopeObjectKindDetail>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<WorkspaceViewPolicy>>]
    [Condition<PollKindConfiguredCondition>]
    [Order(4)]
    [Cache]
    public sealed class PollSidebarLinkFragment : ObjectKindSidebarLinkFragment
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public PollSidebarLinkFragment(IFragmentContext fragmentContext)
            : base(fragmentContext, new PollKind())
        {
        }
    }
}
