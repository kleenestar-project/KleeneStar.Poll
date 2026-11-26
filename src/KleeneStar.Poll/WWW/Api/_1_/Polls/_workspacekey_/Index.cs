using KleeneStar.Core;
using KleeneStar.Core.WebAttribute;
using KleeneStar.Model.Entities;
using KleeneStar.Poll.WebPoll;
using KleeneStar.Poll.WebRestApi;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using PollEntity = KleeneStar.Model.Entities.Poll;

namespace KleeneStar.Poll.WWW.Api._1_.Polls._workspacekey_
{
    /// <summary>
    /// The endpoint of the dialog creating a poll in a workspace. A GET answers the values the
    /// dialog starts from - a scheduling poll in the first poll class the caller may create in;
    /// <c>?type=decision</c> starts a decision. A POST creates the poll and answers its page,
    /// which the dialog follows.
    /// </summary>
    [Title("kleenestar.poll:poll.add.title")]
    [WorkspaceKeySegment]
    [Cache]
    public sealed class Index : IRestApi
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Answers the values the dialog starts from.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The values.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            var workspace = PollRequest.Workspace(request);

            if (workspace is null || !PollAuthorization.MayRead(workspace, request))
            {
                return new ResponseNotFound();
            }

            var classes = PollRequest.Classes(workspace).Where(x => PollAuthorization.MayCreate(x, request)).ToList();
            var values = PollForm.Values(null, null, [], PollRequest.Zone(request), PollRequest.Culture(request));
            var type = PollHub.PollTypeManager.GetType(request.GetParameter("type")?.Value ?? request.GetParameter("id")?.Value);

            values[PollForm.ClassId] = classes.FirstOrDefault()?.Id.ToString() ?? string.Empty;

            if (type is not null)
            {
                values[PollForm.Type] = type.Key;
            }

            return FormAnswer.Values(values);
        }

        /// <summary>
        /// Creates the poll.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The poll page to follow, or the refusals.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Create(IRequest request)
        {
            var workspace = PollRequest.Workspace(request);

            if (workspace is null || !PollAuthorization.MayRead(workspace, request))
            {
                return new ResponseNotFound();
            }

            var payload = FormPayload.Read(request);
            var classes = PollRequest.Classes(workspace).Where(x => PollAuthorization.MayCreate(x, request)).ToList();
            var classId = payload.Id(PollForm.ClassId);
            var @class = classes.FirstOrDefault(x => x.Id == classId) ?? (classId is null ? classes.FirstOrDefault() : null);

            if (@class is null)
            {
                return FormAnswer.Errors(request, new Dictionary<string, string> { [PollForm.ClassId] = "kleenestar.poll:poll.class.required" });
            }

            var errors = new Dictionary<string, string>();
            var draft = PollForm.Read(payload, request, [], null, errors);

            if (errors.Count > 0)
            {
                return FormAnswer.Messages(errors);
            }

            var identityId = PollRequest.Identity(request);
            var entity = PollObjects.Create(@class, draft.Summary, draft.Description, identityId);
            var manager = PollHub.PollManager;

            // the object manager gave the new object default settings; the dialog's replace them
            var poll = manager.GetPoll(entity.Id) ?? new PollEntity { ObjectId = entity.Id };
            PollForm.Apply(draft, poll);
            manager.SavePoll(poll);
            manager.SetOptions(entity.Id, draft.Options);

            CoreHub.AddNotification(I18N.Translate(request, "kleenestar.poll:kind.label"), I18N.Translate(request, "kleenestar.poll:poll.add.done", entity.Key, entity.Summary));

            return FormAnswer.Success(CoreHub.ObjectKindManager.ResolveDetailUri(entity)?.ToString(), new Dictionary<string, object>
            {
                ["data"] = new Dictionary<string, object>
                {
                    ["created"] = true,
                    ["id"] = entity.Id,
                    ["key"] = entity.Key
                }
            });
        }
    }
}
