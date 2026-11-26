using KleeneStar.Core;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Poll.WebPoll
{
    /// <summary>
    /// What the pages, fragments and endpoints of the plugin read from a request: the workspace
    /// and the poll it addresses, the zone its dates are shown in, and the poll classes it can
    /// create in.
    /// </summary>
    public static class PollRequest
    {
        /// <summary>
        /// Resolves the workspace a request addresses: by the workspace key of the route, by the
        /// <c>workspace</c> query parameter, or through the object the route names.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The workspace, or <see langword="null"/>.</returns>
        public static Workspace Workspace(IRequest request)
        {
            var key = request?.GetParameter<WorkspaceKeyParameter>()?.Value;

            if (string.IsNullOrWhiteSpace(key))
            {
                key = request?.GetParameter("workspace")?.Value;
            }

            if (!string.IsNullOrWhiteSpace(key))
            {
                var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(key);

                if (workspace is not null)
                {
                    return workspace;
                }

                if (Guid.TryParse(key, out var workspaceId))
                {
                    return CoreHub.WorkspaceManager.GetWorkspace(workspaceId);
                }
            }

            var entity = AnyObject(request);

            return entity is null ? null : entity.Workspace ?? CoreHub.WorkspaceManager.GetWorkspace(entity.WorkspaceId);
        }

        /// <summary>
        /// Resolves the poll the route of a request names, read through the object manager.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The poll, or <see langword="null"/> when there is none, it is of another
        /// kind, or the caller may not see it.</returns>
        public static ObjectEntity Poll(IRequest request)
        {
            var entity = AnyObject(request);

            return entity is not null && string.Equals(entity.Kind, ObjectKind.Poll, StringComparison.OrdinalIgnoreCase)
                ? entity
                : null;
        }

        /// <summary>
        /// Resolves the object the route of a request names, of whatever kind.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The object, or <see langword="null"/>.</returns>
        public static ObjectEntity AnyObject(IRequest request)
        {
            var key = request?.GetParameter<ObjectKeyParameter>()?.Value;

            return string.IsNullOrWhiteSpace(key) ? null : CoreHub.ObjectManager.GetObjectByKey(key);
        }

        /// <summary>
        /// Returns the account acting in a request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The account, or <see cref="Guid.Empty"/> for nobody.</returns>
        public static Guid Identity(IRequest request)
        {
            return CoreHub.SessionManager?.GetCurrentIdentityId(request) ?? Guid.Empty;
        }

        /// <summary>
        /// Returns the zone the dates of a request are shown and entered in.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The zone.</returns>
        public static TimeZoneInfo Zone(IRequest request)
        {
            return PollHub.PollManager?.GetDisplayZone(Identity(request))
                ?? PollTime.Zone(PollHub.Settings.TimeZone);
        }

        /// <summary>
        /// Returns the culture of a request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The culture.</returns>
        public static CultureInfo Culture(IRequest request)
        {
            return request?.Culture ?? CultureInfo.CurrentCulture;
        }

        /// <summary>
        /// Returns the active, concrete poll classes of a workspace, by name.
        /// </summary>
        /// <param name="workspace">The workspace.</param>
        /// <returns>The classes.</returns>
        public static IReadOnlyList<Class> Classes(Workspace workspace)
        {
            if (workspace is null)
            {
                return [];
            }

            return [.. CoreHub.ClassManager
                .GetClasses(new Query<Class>().WhereEquals(x => x.WorkspaceId, workspace.Id))
                .Where(x => x.State == ClassState.Active && !x.IsAbstract && !x.IsBlueprint)
                .Where(x => string.Equals(x.Kind, ObjectKind.Poll, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)];
        }
    }
}
