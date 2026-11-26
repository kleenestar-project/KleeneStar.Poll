using KleeneStar.Core;
using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebPermissions;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using WebExpress.WebCore.WebMessage;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Poll.WebPoll
{
    /// <summary>
    /// The questions the plugin's pages and endpoints ask before they act - the same ones the
    /// core asks of its own kind pages, because the core's route authorization judges the core's
    /// pages only.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>The polls of a workspace are its content: whoever may read the workspace's
    ///   content may open the list (<see cref="WorkspaceReadContentPermission"/>).</item>
    ///   <item>A poll is an object: it is read where the object manager answers it, and changed
    ///   or closed by whoever may change the object (<see cref="ContentAuthorization.MayWrite"/>).
    ///   Creating one needs the right to create in its class.</item>
    ///   <item>Voting is taking part, like commenting: a signed-in account that may comment on the
    ///   poll (<see cref="ObjectCommentPermission"/>) votes. Nobody (<see cref="Guid.Empty"/>)
    ///   never votes.</item>
    ///   <item>The results are shown to whoever may read the poll, unless the poll hides them
    ///   until it is closed; whoever may change it sees them throughout.</item>
    /// </list>
    /// </remarks>
    public static class PollAuthorization
    {
        /// <summary>
        /// Determines whether the caller may open the polls of a workspace.
        /// </summary>
        /// <param name="workspace">The workspace.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the list may be shown.</returns>
        public static bool MayRead(Workspace workspace, IRequest request)
        {
            return workspace is not null
                && PageAuthorization.IsGranted(request, typeof(WorkspaceReadContentPermission), PageAuthorization.ChainOf(workspace));
        }

        /// <summary>
        /// Refuses a caller who may not open the polls of a workspace.
        /// </summary>
        /// <param name="workspace">The workspace.</param>
        /// <param name="request">The request.</param>
        public static void DemandRead(Workspace workspace, IRequest request)
        {
            if (workspace is not null && !MayRead(workspace, request))
            {
                throw PageAuthorization.Refuse();
            }
        }

        /// <summary>
        /// Determines whether the caller may create a poll in a class.
        /// </summary>
        /// <param name="class">The poll class.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when a poll may be created.</returns>
        public static bool MayCreate(Class @class, IRequest request)
        {
            return @class is not null && ContentAuthorization.MayCreateIn(@class.Id, request);
        }

        /// <summary>
        /// Determines whether the caller may change a poll: its question, options and settings,
        /// and close or open it.
        /// </summary>
        /// <param name="poll">The poll object, read through the object manager.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the poll may be changed.</returns>
        public static bool MayWrite(ObjectEntity poll, IRequest request)
        {
            return poll is not null && ContentAuthorization.MayWrite(poll, request);
        }

        /// <summary>
        /// Refuses a caller who may not change a poll.
        /// </summary>
        /// <param name="poll">The poll object.</param>
        /// <param name="request">The request.</param>
        public static void DemandWrite(ObjectEntity poll, IRequest request)
        {
            if (poll is not null && !MayWrite(poll, request))
            {
                throw PageAuthorization.Refuse();
            }
        }

        /// <summary>
        /// Determines whether the caller may vote in a poll, whether or not it runs.
        /// </summary>
        /// <param name="poll">The poll object, read through the object manager.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the caller may vote.</returns>
        public static bool MayVote(ObjectEntity poll, IRequest request)
        {
            return poll is not null
                && PollRequest.Identity(request) != Guid.Empty
                && ContentAuthorization.MayWrite(poll, request, typeof(ObjectCommentPermission));
        }

        /// <summary>
        /// Refuses a caller who may not vote in a poll.
        /// </summary>
        /// <param name="poll">The poll object.</param>
        /// <param name="request">The request.</param>
        public static void DemandVote(ObjectEntity poll, IRequest request)
        {
            if (poll is not null && !MayVote(poll, request))
            {
                throw PageAuthorization.Refuse();
            }
        }

        /// <summary>
        /// Determines whether the caller sees the results of a poll.
        /// </summary>
        /// <param name="poll">The poll object, read through the object manager.</param>
        /// <param name="settings">The settings of the poll.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the results may be shown.</returns>
        public static bool MaySeeResults(ObjectEntity poll, Model.Entities.Poll settings, IRequest request)
        {
            return poll is not null
                && (settings is null || !settings.HideResults || settings.Closed is not null || MayWrite(poll, request));
        }
    }
}
