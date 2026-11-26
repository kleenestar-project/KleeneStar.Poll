using KleeneStar.Core;
using KleeneStar.Poll.WebPoll;
using KleeneStar.Poll.WebRestApi;
using System.Collections.Generic;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Poll.WWW.Api._1_.Poll._objectkey_
{
    /// <summary>
    /// The endpoint of the dialog opening a closed poll again: a GET names the poll in the
    /// prolog of the dialog, a POST opens it and moves it out of its closing state. The votes
    /// given stay; a deadline that has passed is dropped.
    /// </summary>
    [Title("kleenestar.poll:reopen.title")]
    [Cache]
    public sealed class Reopen : IRestApi
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Reopen()
        {
        }

        /// <summary>
        /// Answers the prolog of the dialog.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The values.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            var entity = PollRequest.Poll(request);

            if (entity is null || !PollAuthorization.MayWrite(entity, request))
            {
                return new ResponseNotFound();
            }

            return FormAnswer.Json(new Dictionary<string, object>
            {
                ["data"] = new Dictionary<string, object>(),
                ["prolog"] = I18N.Translate(request, "kleenestar.poll:reopen.prolog", entity.Summary)
            });
        }

        /// <summary>
        /// Opens the poll again.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The poll page to follow.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Post(IRequest request)
        {
            var entity = PollRequest.Poll(request);

            if (entity is null || PollHub.PollManager.GetPoll(entity.Id) is null)
            {
                return new ResponseNotFound();
            }

            if (!PollAuthorization.MayWrite(entity, request))
            {
                return new ResponseForbidden();
            }

            PollHub.PollManager.Reopen(entity.Id, PollRequest.Identity(request));

            CoreHub.AddNotification(I18N.Translate(request, "kleenestar.poll:kind.label"), I18N.Translate(request, "kleenestar.poll:reopen.done", entity.Key, entity.Summary));

            return FormAnswer.Success(CoreHub.ObjectKindManager.ResolveDetailUri(entity)?.ToString());
        }
    }
}
