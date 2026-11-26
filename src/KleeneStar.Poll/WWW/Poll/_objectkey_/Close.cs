using KleeneStar.Poll.WebPoll;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Poll.WWW.Poll._objectkey_
{
    /// <summary>
    /// The page behind the dialog closing a poll and fixing what it settled on. It is opened as a modal; its form is a fragment scoped
    /// to it.
    /// </summary>
    [WebIcon<IconFlagCheckered>]
    [Title("kleenestar.poll:close.title")]
    [Scope<IScopeGeneral>]
    public sealed class Close : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Close()
        {
        }

        /// <summary>
        /// Refuses a caller who may not use the dialog.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var request = renderContext.Request;

            PollAuthorization.DemandWrite(PollRequest.Poll(request), request);
        }
    }
}
