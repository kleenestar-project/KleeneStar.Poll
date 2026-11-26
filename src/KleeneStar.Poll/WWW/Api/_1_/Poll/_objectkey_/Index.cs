using KleeneStar.Core;
using KleeneStar.Core.WebAttribute;
using KleeneStar.Model.Entities;
using KleeneStar.Poll.WebPoll;
using KleeneStar.Poll.WebRestApi;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using PollEntity = KleeneStar.Model.Entities.Poll;

namespace KleeneStar.Poll.WWW.Api._1_.Poll._objectkey_
{
    /// <summary>
    /// The endpoint of the dialog changing a poll: a GET answers its question, description,
    /// type, options and settings as the dialog shows them; a PUT (or POST) writes what the
    /// dialog submits. An option left as it was keeps its votes; a changed or removed one loses
    /// them, and changing the type removes every vote.
    /// </summary>
    [Title("kleenestar.poll:poll.edit.title")]
    [ObjectKeySegment]
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
            var entity = PollRequest.Poll(request);

            if (entity is null)
            {
                return new ResponseNotFound();
            }

            var manager = PollHub.PollManager;

            return FormAnswer.Values(PollForm.Values
            (
                entity,
                manager.GetPoll(entity.Id),
                manager.GetOptions(entity.Id),
                PollRequest.Zone(request),
                PollRequest.Culture(request)
            ));
        }

        /// <summary>
        /// Writes the dialog.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>A success, or the refusals.</returns>
        [Method(RequestMethod.PUT)]
        [Method(RequestMethod.PATCH)]
        public IResponse Update(IRequest request)
        {
            return Save(request);
        }

        /// <summary>
        /// Writes the dialog (a form that posts).
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>A success, or the refusals.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Post(IRequest request)
        {
            return Save(request);
        }

        /// <summary>
        /// Writes the dialog.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>A success, or the refusals.</returns>
        private static IResponse Save(IRequest request)
        {
            var entity = PollRequest.Poll(request);

            if (entity is null)
            {
                return new ResponseNotFound();
            }

            if (!PollAuthorization.MayWrite(entity, request))
            {
                return new ResponseForbidden();
            }

            var manager = PollHub.PollManager;
            var poll = manager.GetPoll(entity.Id) ?? new PollEntity { ObjectId = entity.Id, Type = DecisionPollType.TypeKey };
            var errors = new Dictionary<string, string>();
            var draft = PollForm.Read(FormPayload.Read(request), request, manager.GetOptions(entity.Id), poll.Deadline, errors);

            if (errors.Count > 0)
            {
                return FormAnswer.Messages(errors);
            }

            var identityId = PollRequest.Identity(request);
            var changed = !string.Equals(entity.Summary, draft.Summary, StringComparison.Ordinal)
                || (draft.Description is not null && !string.Equals(entity.Description ?? string.Empty, draft.Description, StringComparison.Ordinal));

            if (changed)
            {
                entity.Summary = draft.Summary;
                entity.Description = draft.Description ?? entity.Description;
                entity.Updated = DateTime.UtcNow;
                entity.UpdaterId = identityId == Guid.Empty ? null : identityId;

                using (CoreHub.CommitManager.BeginCommit(entity.Id, CommitType.Updated, identityId))
                {
                    CoreHub.ObjectManager.Update(entity);
                }
            }

            PollForm.Apply(draft, poll);
            manager.SavePoll(poll);
            manager.SetOptions(entity.Id, draft.Options);

            CoreHub.AddNotification(I18N.Translate(request, "kleenestar.poll:kind.label"), I18N.Translate(request, "kleenestar.poll:poll.edit.done", entity.Key, entity.Summary));

            return FormAnswer.Success();
        }
    }
}
