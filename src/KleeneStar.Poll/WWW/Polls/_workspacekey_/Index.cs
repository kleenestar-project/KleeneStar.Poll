using KleeneStar.Core;
using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebScope;
using KleeneStar.Core.WebUri;
using KleeneStar.Poll.WebPoll;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Poll.WWW.Polls._workspacekey_
{
    /// <summary>
    /// The polls of a workspace: the running ones with their deadline and turnout, then the
    /// closed ones with their result. The sidebar carries the kind links shared with the core's
    /// kind overviews (the page is an <see cref="IScopeObjectKindOverview"/>) and the running
    /// polls.
    /// </summary>
    [WebIcon<IconSquarePollVertical>]
    [Title("kleenestar.poll:kind.label")]
    [WorkspaceKeySegment]
    [Scope<IScopeGeneral>]
    [Scope<IScopeObjectKindOverview>]
    [Cache]
    public sealed class Index : IPage<VisualTreeWebApp>, IScopeGeneral, IScopeObjectKindOverview
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var request = renderContext.Request;
            var workspace = PollRequest.Workspace(request);

            PollAuthorization.DemandRead(workspace, request);

            visualTree.BreadcrumbUri = renderContext.PageContext.ApplicationContext.Route
                .Concat(new WorkspaceKeyUriPathSegmentVariable<WorkspaceKeyParameter>()
                {
                    Value = workspace?.Key,
                    Uri = CoreHub.GetUri<Index>()
                        .BindParameters(new WorkspaceKeyParameter(workspace?.Key))
                })
                .ToUri()
                .BindParameters(request);

            visualTree.Title = workspace?.Name;
            visualTree.Content.MainPanel.Headline.Title = I18N.Translate(request, "kleenestar.poll:kind.label");

            // the polls are workspace content, so they advance the workspace's ranking like the
            // core's kind overviews
            if (workspace is not null)
            {
                CoreHub.WorkspaceManager.RecordVisit(PollRequest.Identity(request), workspace.Id);
            }
        }
    }
}
