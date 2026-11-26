using KleeneStar.Core;
using KleeneStar.Poll.WebPoll;
using KleeneStar.Poll.WebRestApi;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Poll.WWW.Api._1_.Poll._objectkey_
{
    /// <summary>
    /// The endpoint of the dialog closing a poll: a GET proposes the leading option as what the
    /// poll settled on, a POST closes the poll with the option chosen (or none) and moves it
    /// into the first closing state of its workflow.
    /// </summary>
    [Title("kleenestar.poll:close.title")]
    [Cache]
    public sealed class Close : IRestApi
    {
        /// <summary>Input name of the option the poll settled on.</summary>
        public const string ChosenField = "Chosen";

        /// <summary>The value of the choice "none of the options".</summary>
        public const string NoneValue = "none";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Close()
        {
        }

        /// <summary>
        /// Answers the option the dialog proposes.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The values.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            var entity = PollRequest.Poll(request);
            var result = entity is null ? null : PollHub.PollManager.GetResult(entity.Id);

            if (result is null || !PollAuthorization.MayWrite(entity, request))
            {
                return new ResponseNotFound();
            }

            var proposed = result.Poll.ChosenOptionId ?? result.Leaders.FirstOrDefault()?.Option.Id;

            return FormAnswer.Values(new Dictionary<string, object>
            {
                [ChosenField] = proposed?.ToString("N") ?? NoneValue
            });
        }

        /// <summary>
        /// Closes the poll.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The poll page to follow, or the refusals.</returns>
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

            var text = FormPayload.Read(request).Text(ChosenField);
            Guid? chosen = Guid.TryParse(text, out var id) ? id : null;

            if (chosen is not null && !PollHub.PollManager.GetOptions(entity.Id).Any(x => x.Id == chosen))
            {
                return FormAnswer.Errors(request, new Dictionary<string, string> { [ChosenField] = "kleenestar.poll:close.chosen.invalid" });
            }

            // "none" closes without fixing an option, also where one leads
            var takeLeader = !string.Equals(text, NoneValue, StringComparison.OrdinalIgnoreCase);

            PollHub.PollManager.Close(entity.Id, chosen, PollRequest.Identity(request), takeLeader);

            CoreHub.AddNotification(I18N.Translate(request, "kleenestar.poll:kind.label"), I18N.Translate(request, "kleenestar.poll:close.done", entity.Key, entity.Summary));

            return FormAnswer.Success(CoreHub.ObjectKindManager.ResolveDetailUri(entity)?.ToString());
        }
    }
}
