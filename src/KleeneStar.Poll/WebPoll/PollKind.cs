using KleeneStar.Core;
using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebParameter;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Poll.WebPoll
{
    /// <summary>
    /// The poll kind: scheduling polls and decisions of a workspace. The overview lists the polls
    /// (<c>/polls/{workspacekey}</c>), the detail view is the poll page
    /// (<c>/poll/{objectkey}</c>), which carries the core's full object detail experience beside
    /// the options, the votes and the result.
    /// </summary>
    public sealed class PollKind : IObjectKind
    {
        /// <summary>
        /// Gets the persisted kind key of polls.
        /// </summary>
        public string Key => Model.Entities.ObjectKind.Poll;

        /// <summary>
        /// Gets the internationalization key of the display name.
        /// </summary>
        public string Label => "kleenestar.poll:kind.label";

        /// <summary>
        /// Gets the icon representing polls.
        /// </summary>
        public IIcon Icon => new IconSquarePollVertical();

        /// <summary>
        /// Gets the display order; the polls sit below the calendar (3) and above the issues (5).
        /// </summary>
        public int Order => 4;

        /// <summary>
        /// Gets <see langword="false"/>: a poll is answered by many, nobody waits on it, so no
        /// SLA times it.
        /// </summary>
        public bool ServiceLevels => false;

        /// <summary>
        /// Gets the unbound route of the polls of a workspace.
        /// </summary>
        public IUri OverviewUri => CoreHub.GetUri<global::KleeneStar.Poll.WWW.Polls._workspacekey_.Index>();

        /// <summary>
        /// Returns the poll page bound to the supplied object key.
        /// </summary>
        /// <param name="objectKey">The key of the poll.</param>
        /// <returns>The bound detail route.</returns>
        public IUri DetailUri(string objectKey) => CoreHub
            .GetUri<global::KleeneStar.Poll.WWW.Poll._objectkey_.Index>()?
            .BindParameters(new ObjectKeyParameter(objectKey));

        /// <summary>
        /// Returns <see langword="null"/>: a poll is edited through dialogs opened from its page.
        /// </summary>
        /// <param name="objectKey">The key of the poll (unused).</param>
        /// <returns>Always <see langword="null"/>.</returns>
        public IUri EditUri(string objectKey) => null;
    }

    /// <summary>
    /// Gates the sidebar link of the polls: it is shown where the workspace has a class of the
    /// poll kind.
    /// </summary>
    public sealed class PollKindConfiguredCondition : ObjectKindConfiguredCondition
    {
        /// <inheritdoc/>
        protected override string Kind => Model.Entities.ObjectKind.Poll;
    }
}
