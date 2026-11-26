using KleeneStar.Core;
using KleeneStar.Model.Entities;
using KleeneStar.Poll.WebPoll;
using KleeneStar.Poll.WebRestApi;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using PollEntity = KleeneStar.Model.Entities.Poll;

namespace KleeneStar.Poll.WWW.Api._1_.Poll._objectkey_
{
    /// <summary>
    /// The endpoint of the vote of the caller: a GET answers the vote given so far, a POST
    /// replaces it. A vote without any answer withdraws the caller's vote.
    /// </summary>
    /// <remarks>
    /// The inputs depend on the type of the poll:
    /// <list type="bullet">
    ///   <item>a graded poll (a scheduling poll) answers every option in
    ///   <c>Answer_{option}</c> with <c>yes</c>, <c>maybe</c> or <c>no</c>; a blank input leaves
    ///   the option unanswered;</item>
    ///   <item>a decision allowing one choice names it in <c>Choice</c>;</item>
    ///   <item>a decision allowing several checks <c>Choice_{option}</c> per option chosen.</item>
    /// </list>
    /// The option ids are written without dashes (<c>N</c> format).
    /// </remarks>
    [Title("kleenestar.poll:vote.title")]
    [Cache]
    public sealed class Vote : IRestApi
    {
        /// <summary>Prefix of the inputs answering one option of a graded poll.</summary>
        public const string AnswerPrefix = "Answer_";

        /// <summary>Input name of the single choice of a decision.</summary>
        public const string ChoiceField = "Choice";

        /// <summary>Prefix of the inputs of the several choices of a decision.</summary>
        public const string ChoicePrefix = "Choice_";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Vote()
        {
        }

        /// <summary>
        /// Answers the vote given so far.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The values.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            var entity = PollRequest.Poll(request);
            var poll = entity is null ? null : PollHub.PollManager.GetPoll(entity.Id);

            if (poll is null || !PollAuthorization.MayVote(entity, request))
            {
                return new ResponseNotFound();
            }

            var identityId = PollRequest.Identity(request);
            var type = PollHub.PollTypeManager.Resolve(poll.Type);
            var options = PollHub.PollManager.GetOptions(entity.Id);
            var votes = PollHub.PollManager.GetVotes(entity.Id).Where(x => x.IdentityId == identityId).ToDictionary(x => x.OptionId, x => x.Answer);

            return FormAnswer.Values(Values(poll, type, options, votes));
        }

        /// <summary>
        /// Writes the vote.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The poll page to follow, or the refusals.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Post(IRequest request)
        {
            var entity = PollRequest.Poll(request);
            var poll = entity is null ? null : PollHub.PollManager.GetPoll(entity.Id);

            if (poll is null)
            {
                return new ResponseNotFound();
            }

            if (!PollAuthorization.MayVote(entity, request))
            {
                return new ResponseForbidden();
            }

            if (!PollHub.PollManager.IsRunning(poll, DateTime.UtcNow))
            {
                return FormAnswer.Refuse(I18N.Translate(request, "kleenestar.poll:vote.closed"));
            }

            var type = PollHub.PollTypeManager.Resolve(poll.Type);
            var options = PollHub.PollManager.GetOptions(entity.Id);
            var answers = Read(FormPayload.Read(request), poll, type, options);

            if (!type.Graded && !poll.MultipleChoice && answers.Count > 1)
            {
                return FormAnswer.Errors(request, new Dictionary<string, string> { [ChoiceField] = "kleenestar.poll:vote.one" });
            }

            try
            {
                PollHub.PollManager.Vote(entity.Id, PollRequest.Identity(request), answers);
            }
            catch (InvalidOperationException)
            {
                return FormAnswer.Refuse(I18N.Translate(request, "kleenestar.poll:vote.invalid"));
            }

            CoreHub.AddNotification
            (
                I18N.Translate(request, "kleenestar.poll:kind.label"),
                I18N.Translate(request, answers.Count == 0 ? "kleenestar.poll:vote.withdrawn" : "kleenestar.poll:vote.done", entity.Summary)
            );

            return FormAnswer.Success(CoreHub.ObjectKindManager.ResolveDetailUri(entity)?.ToString());
        }

        /// <summary>
        /// Answers the values of the vote dialog.
        /// </summary>
        /// <param name="poll">The settings of the poll.</param>
        /// <param name="type">The type of the poll.</param>
        /// <param name="options">The options.</param>
        /// <param name="votes">The answers of the caller, keyed by option.</param>
        /// <returns>The values, keyed by input name.</returns>
        public static Dictionary<string, object> Values(PollEntity poll, IPollType type, IReadOnlyList<PollOption> options, IReadOnlyDictionary<Guid, PollAnswer> votes)
        {
            var values = new Dictionary<string, object>();

            if (type.Graded)
            {
                foreach (var option in options)
                {
                    values[AnswerPrefix + option.Id.ToString("N")] = votes.TryGetValue(option.Id, out var answer)
                        ? answer.ToString().ToLowerInvariant()
                        : string.Empty;
                }
            }
            else if (poll.MultipleChoice)
            {
                foreach (var option in options)
                {
                    values[ChoicePrefix + option.Id.ToString("N")] = votes.TryGetValue(option.Id, out var answer) && answer == PollAnswer.Yes;
                }
            }
            else
            {
                values[ChoiceField] = options.FirstOrDefault(x => votes.TryGetValue(x.Id, out var answer) && answer == PollAnswer.Yes)?.Id.ToString("N") ?? string.Empty;
            }

            return values;
        }

        /// <summary>
        /// Reads the answers of a submitted vote dialog.
        /// </summary>
        /// <param name="payload">The payload.</param>
        /// <param name="poll">The settings of the poll.</param>
        /// <param name="type">The type of the poll.</param>
        /// <param name="options">The options.</param>
        /// <returns>The answers, keyed by option.</returns>
        public static Dictionary<Guid, PollAnswer> Read(FormPayload payload, PollEntity poll, IPollType type, IReadOnlyList<PollOption> options)
        {
            var answers = new Dictionary<Guid, PollAnswer>();

            if (type.Graded)
            {
                foreach (var option in options)
                {
                    var text = payload.Text(AnswerPrefix + option.Id.ToString("N"));

                    if (Enum.TryParse<PollAnswer>(text, true, out var answer) && Enum.IsDefined(answer) && PollResult.Allowed(poll, type, answer))
                    {
                        answers[option.Id] = answer;
                    }
                }
            }
            else if (poll.MultipleChoice)
            {
                foreach (var option in options)
                {
                    if (payload.Flag(ChoicePrefix + option.Id.ToString("N")))
                    {
                        answers[option.Id] = PollAnswer.Yes;
                    }
                }
            }
            else if (Guid.TryParse(payload.Text(ChoiceField), out var chosen) && options.Any(x => x.Id == chosen))
            {
                answers[chosen] = PollAnswer.Yes;
            }

            return answers;
        }
    }
}
