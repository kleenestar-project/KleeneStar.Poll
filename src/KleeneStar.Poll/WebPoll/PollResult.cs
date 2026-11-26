using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using PollEntity = KleeneStar.Model.Entities.Poll;

namespace KleeneStar.Poll.WebPoll
{
    /// <summary>
    /// The count of one option of a poll.
    /// </summary>
    public sealed class PollOptionResult
    {
        /// <summary>
        /// Gets the option.
        /// </summary>
        public PollOption Option { get; init; }

        /// <summary>
        /// Gets the accounts that answered yes (or chose the option).
        /// </summary>
        public IReadOnlyList<Guid> Yes { get; init; } = [];

        /// <summary>
        /// Gets the accounts that answered if need be.
        /// </summary>
        public IReadOnlyList<Guid> Maybe { get; init; } = [];

        /// <summary>
        /// Gets the accounts that answered no.
        /// </summary>
        public IReadOnlyList<Guid> No { get; init; } = [];

        /// <summary>
        /// Gets the share of the voters that answered yes or if need be, from 0 to 100.
        /// </summary>
        public int Percent { get; init; }

        /// <summary>
        /// Gets whether the option leads the poll (several may lead together).
        /// </summary>
        public bool Leading { get; init; }
    }

    /// <summary>
    /// The result of a poll: per option who answered what, how many voted, and which options
    /// lead.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A decision is won by the option chosen most often. A scheduling poll is won by the date
    /// most voters can make (yes or if need be); among those, by the one with the most plain
    /// yes. An option nobody answered positively leads nothing.
    /// </para>
    /// <para>
    /// Only votes naming an option of the poll and an answer the poll allows are counted, so a
    /// poll changed after it was answered still counts what is valid.
    /// </para>
    /// </remarks>
    public sealed class PollResult
    {
        /// <summary>
        /// Gets the poll.
        /// </summary>
        public PollEntity Poll { get; init; }

        /// <summary>
        /// Gets the type of the poll.
        /// </summary>
        public IPollType Type { get; init; }

        /// <summary>
        /// Gets the counts of the options, in the poll's order.
        /// </summary>
        public IReadOnlyList<PollOptionResult> Options { get; init; } = [];

        /// <summary>
        /// Gets the accounts that voted, in the order of their first vote.
        /// </summary>
        public IReadOnlyList<Guid> Voters { get; init; } = [];

        /// <summary>
        /// Gets the leading options; empty before anybody answered positively.
        /// </summary>
        public IReadOnlyList<PollOptionResult> Leaders => [.. Options.Where(x => x.Leading)];

        /// <summary>
        /// Returns the answer an account gave to an option.
        /// </summary>
        /// <param name="optionId">The option.</param>
        /// <param name="identityId">The account.</param>
        /// <returns>The answer, or <see langword="null"/> when it gave none.</returns>
        public PollAnswer? AnswerOf(Guid optionId, Guid identityId)
        {
            var option = Options.FirstOrDefault(x => x.Option.Id == optionId);

            if (option is null)
            {
                return null;
            }

            return option.Yes.Contains(identityId) ? PollAnswer.Yes
                : option.Maybe.Contains(identityId) ? PollAnswer.Maybe
                : option.No.Contains(identityId) ? PollAnswer.No
                : null;
        }

        /// <summary>
        /// Counts the votes of a poll.
        /// </summary>
        /// <param name="poll">The settings of the poll.</param>
        /// <param name="type">The type of the poll.</param>
        /// <param name="options">The options, in their order.</param>
        /// <param name="votes">The votes.</param>
        /// <returns>The result.</returns>
        public static PollResult Tally(PollEntity poll, IPollType type, IReadOnlyList<PollOption> options, IReadOnlyList<PollVote> votes)
        {
            ArgumentNullException.ThrowIfNull(poll);
            ArgumentNullException.ThrowIfNull(type);

            options ??= [];

            var known = options.Select(x => x.Id).ToHashSet();
            var valid = (votes ?? [])
                .Where(x => known.Contains(x.OptionId))
                .Where(x => Allowed(poll, type, x.Answer))
                .ToList();

            var voters = valid
                .Where(x => type.Graded || x.Answer == PollAnswer.Yes)
                .Select(x => x.IdentityId)
                .Distinct()
                .ToList();

            var counted = options.Select(option =>
            {
                var of = valid.Where(x => x.OptionId == option.Id).ToList();

                return new
                {
                    Option = option,
                    Yes = of.Where(x => x.Answer == PollAnswer.Yes).Select(x => x.IdentityId).Distinct().ToList(),
                    Maybe = of.Where(x => x.Answer == PollAnswer.Maybe).Select(x => x.IdentityId).Distinct().ToList(),
                    No = of.Where(x => x.Answer == PollAnswer.No).Select(x => x.IdentityId).Distinct().ToList()
                };
            }).ToList();

            (int, int) Score(int yes, int maybe) => type.Graded ? (yes + maybe, yes) : (yes, 0);

            var best = counted
                .Select(x => Score(x.Yes.Count, x.Maybe.Count))
                .Where(x => x.Item1 > 0)
                .DefaultIfEmpty((0, 0))
                .Max();

            return new PollResult
            {
                Poll = poll,
                Type = type,
                Voters = voters,
                Options = [.. counted.Select(x => new PollOptionResult
                {
                    Option = x.Option,
                    Yes = x.Yes,
                    Maybe = x.Maybe,
                    No = x.No,
                    Percent = voters.Count == 0 ? 0 : (int)Math.Round(100.0 * (x.Yes.Count + x.Maybe.Count) / voters.Count),
                    Leading = best.Item1 > 0 && Score(x.Yes.Count, x.Maybe.Count) == best
                })]
            };
        }

        /// <summary>
        /// Determines whether a poll allows an answer.
        /// </summary>
        /// <param name="poll">The settings of the poll.</param>
        /// <param name="type">The type of the poll.</param>
        /// <param name="answer">The answer.</param>
        /// <returns><see langword="true"/> when the answer counts.</returns>
        public static bool Allowed(PollEntity poll, IPollType type, PollAnswer answer)
        {
            return answer switch
            {
                PollAnswer.Yes => true,
                PollAnswer.Maybe => type.Graded && poll.AllowMaybe,
                PollAnswer.No => type.Graded,
                _ => false
            };
        }
    }
}
