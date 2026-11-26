using KleeneStar.Model.Entities;
using KleeneStar.Poll.WebPoll;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using PollEntity = KleeneStar.Model.Entities.Poll;

namespace KleeneStar.Poll.WebManager
{
    /// <summary>
    /// A poll that still runs, as the sidebar lists it.
    /// </summary>
    public sealed class RunningPoll
    {
        /// <summary>
        /// Gets the poll object, read through the object manager.
        /// </summary>
        public Model.Entities.Object Object { get; init; }

        /// <summary>
        /// Gets the settings of the poll.
        /// </summary>
        public PollEntity Poll { get; init; }

        /// <summary>
        /// Gets whether the account asking has voted already.
        /// </summary>
        public bool Voted { get; init; }
    }

    /// <summary>
    /// A ballot as the audit log records it: who voted in which poll and how many answers they
    /// gave, but not which - an anonymous poll stays anonymous in the log.
    /// </summary>
    public sealed class PollBallot
    {
        /// <summary>
        /// Gets the poll object.
        /// </summary>
        public Guid ObjectId { get; init; }

        /// <summary>
        /// Gets the account that voted.
        /// </summary>
        public Guid IdentityId { get; init; }

        /// <summary>
        /// Gets the number of answers given; zero for a vote withdrawn.
        /// </summary>
        public int Answers { get; init; }
    }

    /// <summary>
    /// Manages what the poll kind adds to an object: its settings, its options, the votes and
    /// the result, and the closing of a poll - by hand, when its workflow reaches a closing
    /// state, or when its deadline runs out.
    /// </summary>
    /// <remarks>
    /// The poll object itself is the core's: it is created, changed and removed through the
    /// object manager. Every read of objects here goes through that manager too, so a list of
    /// polls shows each reader only what that reader may see.
    /// </remarks>
    public interface IPollManager : IComponentManager
    {
        /// <summary>
        /// Raised after the settings of a poll were written.
        /// </summary>
        event EventHandler<PollEntity> PollChanged;

        /// <summary>
        /// Raised after the options of a poll were replaced.
        /// </summary>
        event EventHandler<Guid> OptionsChanged;

        /// <summary>
        /// Raised after an account voted or withdrew its vote.
        /// </summary>
        event EventHandler<PollBallot> Voted;

        /// <summary>
        /// Raised after a poll was closed.
        /// </summary>
        event EventHandler<PollEntity> PollClosed;

        /// <summary>
        /// Raised after a closed poll was opened again.
        /// </summary>
        event EventHandler<PollEntity> PollReopened;

        /// <summary>
        /// Subscribes to the core's object and workflow events and to the audit log. Called once
        /// by the plugin; further calls do nothing.
        /// </summary>
        void Connect();

        /// <summary>
        /// Returns the zone the dates of an account are shown and entered in: the account's own,
        /// else the plugin's setting, else the host's.
        /// </summary>
        /// <param name="identityId">The account, or <see cref="Guid.Empty"/>.</param>
        /// <returns>The zone.</returns>
        TimeZoneInfo GetDisplayZone(Guid identityId);

        /// <summary>
        /// Returns the settings of a poll.
        /// </summary>
        /// <param name="objectId">The poll object.</param>
        /// <returns>The settings, or <see langword="null"/>.</returns>
        PollEntity GetPoll(Guid objectId);

        /// <summary>
        /// Returns the settings of several polls in one read.
        /// </summary>
        /// <param name="objectIds">The poll objects.</param>
        /// <returns>The settings found, keyed by object.</returns>
        IReadOnlyDictionary<Guid, PollEntity> GetPolls(IEnumerable<Guid> objectIds);

        /// <summary>
        /// Writes the settings of a poll. Changing the type removes every vote, since what was
        /// answered no longer exists; the closing state is changed by <see cref="Close"/> and
        /// <see cref="Reopen"/> only.
        /// </summary>
        /// <param name="poll">The settings.</param>
        /// <returns>The settings as stored.</returns>
        PollEntity SavePoll(PollEntity poll);

        /// <summary>
        /// Returns the options of a poll in their order.
        /// </summary>
        /// <param name="objectId">The poll object.</param>
        /// <returns>The options.</returns>
        IReadOnlyList<PollOption> GetOptions(Guid objectId);

        /// <summary>
        /// Replaces the options of a poll. An option keeping its id keeps its votes.
        /// </summary>
        /// <param name="objectId">The poll object.</param>
        /// <param name="options">The options, in their order.</param>
        /// <returns>The options as stored.</returns>
        IReadOnlyList<PollOption> SetOptions(Guid objectId, IEnumerable<PollOption> options);

        /// <summary>
        /// Returns the votes of a poll.
        /// </summary>
        /// <param name="objectId">The poll object.</param>
        /// <returns>The votes.</returns>
        IReadOnlyList<PollVote> GetVotes(Guid objectId);

        /// <summary>
        /// Returns how many accounts voted in each of several polls, in one read.
        /// </summary>
        /// <param name="objectIds">The poll objects.</param>
        /// <returns>The numbers of voters, keyed by object; a poll without votes is missing.</returns>
        IReadOnlyDictionary<Guid, int> GetVoterCounts(IEnumerable<Guid> objectIds);

        /// <summary>
        /// Returns the polls among several an account has voted in, in one read.
        /// </summary>
        /// <param name="objectIds">The poll objects.</param>
        /// <param name="identityId">The account.</param>
        /// <returns>The poll objects the account voted in.</returns>
        IReadOnlySet<Guid> GetVotedBy(IEnumerable<Guid> objectIds, Guid identityId);

        /// <summary>
        /// Counts the votes of a poll.
        /// </summary>
        /// <param name="objectId">The poll object.</param>
        /// <returns>The result, or <see langword="null"/> for an object that is no poll.</returns>
        PollResult GetResult(Guid objectId);

        /// <summary>
        /// Determines whether a poll takes votes at a point in time: it is not closed and its
        /// deadline, if any, lies after that point.
        /// </summary>
        /// <param name="poll">The settings of the poll.</param>
        /// <param name="now">The point in time (UTC).</param>
        /// <returns><see langword="true"/> while the poll runs.</returns>
        bool IsRunning(PollEntity poll, DateTime now);

        /// <summary>
        /// Writes the vote of an account, replacing the one it gave before.
        /// </summary>
        /// <param name="objectId">The poll object.</param>
        /// <param name="identityId">The account voting; never <see cref="Guid.Empty"/>.</param>
        /// <param name="answers">The answers, keyed by option; empty to withdraw the vote.</param>
        /// <returns>The votes of the account as stored.</returns>
        /// <exception cref="InvalidOperationException">For nobody, a poll that does not run, an
        /// option of another poll, an answer the poll does not allow, or several choices in a
        /// poll allowing one.</exception>
        IReadOnlyList<PollVote> Vote(Guid objectId, Guid identityId, IReadOnlyDictionary<Guid, PollAnswer> answers);

        /// <summary>
        /// Closes a poll and moves it into the first closing state its workflow leads to.
        /// </summary>
        /// <param name="objectId">The poll object.</param>
        /// <param name="chosenOptionId">The option the poll settled on, or <see langword="null"/>.</param>
        /// <param name="actorId">Who closes it; <see cref="Guid.Empty"/> for the system.</param>
        /// <param name="takeLeader">Whether a poll closed without a chosen option settles on its
        /// single leading option, if there is one.</param>
        /// <returns>The settings as stored, or <see langword="null"/> for an object that is no poll.</returns>
        /// <exception cref="InvalidOperationException">For an option of another poll.</exception>
        PollEntity Close(Guid objectId, Guid? chosenOptionId, Guid actorId, bool takeLeader = true);

        /// <summary>
        /// Opens a closed poll again and moves it out of its closing state. A deadline that has
        /// passed is dropped, so the poll is not closed again at once.
        /// </summary>
        /// <param name="objectId">The poll object.</param>
        /// <param name="actorId">Who opens it.</param>
        /// <returns>The settings as stored, or <see langword="null"/> for an object that is no poll.</returns>
        PollEntity Reopen(Guid objectId, Guid actorId);

        /// <summary>
        /// Returns the running polls of a workspace the caller may see, the next deadline first,
        /// then the newest; polls whose workflow reached a closing state are left out.
        /// </summary>
        /// <param name="workspace">The workspace.</param>
        /// <param name="identityId">The account asking, for whether it voted.</param>
        /// <param name="now">The present (UTC).</param>
        /// <param name="limit">The most polls answered.</param>
        /// <returns>The running polls.</returns>
        IReadOnlyList<RunningPoll> GetRunning(Workspace workspace, Guid identityId, DateTime now, int limit);

        /// <summary>
        /// Closes the polls whose deadline ran out.
        /// </summary>
        /// <param name="now">The present (UTC).</param>
        /// <returns>The number of polls closed.</returns>
        int CloseExpired(DateTime now);
    }
}
