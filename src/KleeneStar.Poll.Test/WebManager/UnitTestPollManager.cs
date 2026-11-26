using KleeneStar.Model.Entities;
using KleeneStar.Poll.WebPoll;
using ObjectEntity = KleeneStar.Model.Entities.Object;
using PollEntity = KleeneStar.Model.Entities.Poll;

namespace KleeneStar.Poll.Test.WebManager
{
    /// <summary>
    /// Tests the poll manager on an in-memory database: settings, options, votes, the result,
    /// closing and opening, the deadline and the running polls of a workspace.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestPollManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("B0110000-0000-4000-8000-000000000001");
        private static readonly Guid OtherWorkspaceId = Guid.Parse("B0110000-0000-4000-8000-000000000002");
        private static readonly Guid ClassId = Guid.Parse("B0110000-0000-4000-8000-000000000003");
        private static readonly Guid OtherClassId = Guid.Parse("B0110000-0000-4000-8000-000000000004");
        private static readonly Guid AliceId = Guid.Parse("B0110000-0000-4000-8000-000000000005");
        private static readonly Guid BobId = Guid.Parse("B0110000-0000-4000-8000-000000000006");
        private static readonly Guid CarolId = Guid.Parse("B0110000-0000-4000-8000-000000000007");

        private static readonly DateTime Monday = new(2030, 3, 4, 9, 0, 0, DateTimeKind.Utc);

        private static void Seed(string database)
        {
            PollHubFixture.Initialize(database);

            using var db = PollHubFixture.CreateDbContext(database);

            if (db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                return;
            }

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "TEAM", Name = "Team" });
            db.Workspaces.Add(new Workspace { Id = OtherWorkspaceId, Key = "OPS", Name = "Operations" });
            db.Classes.Add(new Class { Id = ClassId, Name = "Poll", WorkspaceId = WorkspaceId, Kind = ObjectKind.Poll });
            db.Classes.Add(new Class { Id = OtherClassId, Name = "Poll", WorkspaceId = OtherWorkspaceId, Kind = ObjectKind.Poll });
            db.Identities.Add(new Identity { Id = AliceId, Name = "Alice", UserName = "alice", Email = "alice@example.com", PasswordHash = "x" });
            db.Identities.Add(new Identity { Id = BobId, Name = "Bob", UserName = "bob", Email = "bob@example.com", PasswordHash = "x" });
            db.Identities.Add(new Identity { Id = CarolId, Name = "Carol", UserName = "carol", Email = "carol@example.com", PasswordHash = "x" });
            db.SaveChanges();
        }

        /// <summary>
        /// Creates a poll object with its settings and options.
        /// </summary>
        private static (Guid Id, IReadOnlyList<PollOption> Options) Create(string database, string type, Guid workspaceId, string summary, int options, Action<PollEntity> configure = null)
        {
            var id = Guid.NewGuid();

            using (var db = PollHubFixture.CreateDbContext(database))
            {
                db.Objects.Add(new ObjectEntity(id)
                {
                    Key = "P-" + Random.Shared.Next(1000, 99999),
                    Summary = summary,
                    WorkspaceId = workspaceId,
                    ClassId = workspaceId == WorkspaceId ? ClassId : OtherClassId,
                    Kind = ObjectKind.Poll,
                    State = WorkspaceState.Active,
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow
                });
                db.SaveChanges();
            }

            var poll = new PollEntity { ObjectId = id, Type = type, AllowMaybe = true, TimeZone = "UTC" };
            configure?.Invoke(poll);
            PollHub.PollManager.SavePoll(poll);

            var stored = PollHub.PollManager.SetOptions(id, Enumerable.Range(0, options).Select(i => type == SchedulePollType.TypeKey
                ? new PollOption { Start = Monday.AddDays(i), End = Monday.AddDays(i).AddHours(1) }
                : new PollOption { Text = "Option " + (char)('A' + i) }));

            return (id, stored);
        }

        private static Workspace Team(string database)
        {
            using var db = PollHubFixture.CreateDbContext(database);

            return db.Workspaces.First(x => x.Id == WorkspaceId);
        }

        /// <summary>
        /// A scheduling poll is won by the date most voters can make; among those by the most
        /// plain yes.
        /// </summary>
        [Fact]
        public void ScheduleLeaderIsTheDateMostCanMake()
        {
            var database = nameof(ScheduleLeaderIsTheDateMostCanMake);
            Seed(database);

            var (id, options) = Create(database, SchedulePollType.TypeKey, WorkspaceId, "When?", 3);
            var manager = PollHub.PollManager;

            manager.Vote(id, AliceId, new Dictionary<Guid, PollAnswer> { [options[0].Id] = PollAnswer.Yes, [options[1].Id] = PollAnswer.Yes, [options[2].Id] = PollAnswer.No });
            manager.Vote(id, BobId, new Dictionary<Guid, PollAnswer> { [options[0].Id] = PollAnswer.No, [options[1].Id] = PollAnswer.Maybe, [options[2].Id] = PollAnswer.Yes });
            manager.Vote(id, CarolId, new Dictionary<Guid, PollAnswer> { [options[0].Id] = PollAnswer.Yes, [options[1].Id] = PollAnswer.Maybe });

            var result = manager.GetResult(id);

            Assert.Equal(3, result.Voters.Count);
            Assert.Single(result.Leaders);
            Assert.Equal(options[1].Id, result.Leaders[0].Option.Id);
            Assert.Equal(100, result.Options[1].Percent);
            Assert.Equal(PollAnswer.Maybe, result.AnswerOf(options[1].Id, BobId));
            Assert.Null(result.AnswerOf(options[2].Id, CarolId));
        }

        /// <summary>
        /// A decision is won by the option chosen most often; a tie leads together.
        /// </summary>
        [Fact]
        public void DecisionLeadersAreTheMostChosen()
        {
            var database = nameof(DecisionLeadersAreTheMostChosen);
            Seed(database);

            var (id, options) = Create(database, DecisionPollType.TypeKey, WorkspaceId, "Which?", 3);
            var manager = PollHub.PollManager;

            manager.Vote(id, AliceId, new Dictionary<Guid, PollAnswer> { [options[0].Id] = PollAnswer.Yes });
            manager.Vote(id, BobId, new Dictionary<Guid, PollAnswer> { [options[1].Id] = PollAnswer.Yes });

            var result = manager.GetResult(id);

            Assert.Equal(2, result.Leaders.Count);
            Assert.Equal(50, result.Options[0].Percent);
            Assert.Equal(0, result.Options[2].Percent);

            manager.Vote(id, CarolId, new Dictionary<Guid, PollAnswer> { [options[1].Id] = PollAnswer.Yes });

            Assert.Equal(options[1].Id, Assert.Single(manager.GetResult(id).Leaders).Option.Id);
        }

        /// <summary>
        /// A vote replaces the one given before; a vote without answers withdraws it.
        /// </summary>
        [Fact]
        public void VoteReplacesAndWithdraws()
        {
            var database = nameof(VoteReplacesAndWithdraws);
            Seed(database);

            var (id, options) = Create(database, DecisionPollType.TypeKey, WorkspaceId, "Which?", 2);
            var manager = PollHub.PollManager;

            manager.Vote(id, AliceId, new Dictionary<Guid, PollAnswer> { [options[0].Id] = PollAnswer.Yes });
            manager.Vote(id, AliceId, new Dictionary<Guid, PollAnswer> { [options[1].Id] = PollAnswer.Yes });

            var votes = manager.GetVotes(id);
            Assert.Equal(options[1].Id, Assert.Single(votes).OptionId);

            manager.Vote(id, AliceId, new Dictionary<Guid, PollAnswer>());

            Assert.Empty(manager.GetVotes(id));
            Assert.Empty(manager.GetResult(id).Voters);
        }

        /// <summary>
        /// A vote is refused for nobody, an option of another poll, an answer the poll does not
        /// allow, several choices where one is allowed, and a poll that is closed or ran out.
        /// </summary>
        [Fact]
        public void VoteRefusesWhatThePollDoesNotAllow()
        {
            var database = nameof(VoteRefusesWhatThePollDoesNotAllow);
            Seed(database);

            var (decision, choices) = Create(database, DecisionPollType.TypeKey, WorkspaceId, "Which?", 2);
            var (schedule, dates) = Create(database, SchedulePollType.TypeKey, WorkspaceId, "When?", 2, x => x.AllowMaybe = false);
            var (expired, late) = Create(database, DecisionPollType.TypeKey, WorkspaceId, "Late?", 2, x => x.Deadline = DateTime.UtcNow.AddMinutes(-1));
            var manager = PollHub.PollManager;

            Assert.Throws<InvalidOperationException>(() => manager.Vote(decision, Guid.Empty, new Dictionary<Guid, PollAnswer> { [choices[0].Id] = PollAnswer.Yes }));
            Assert.Throws<InvalidOperationException>(() => manager.Vote(decision, AliceId, new Dictionary<Guid, PollAnswer> { [dates[0].Id] = PollAnswer.Yes }));
            Assert.Throws<InvalidOperationException>(() => manager.Vote(decision, AliceId, new Dictionary<Guid, PollAnswer> { [choices[0].Id] = PollAnswer.No }));
            Assert.Throws<InvalidOperationException>(() => manager.Vote(decision, AliceId, new Dictionary<Guid, PollAnswer> { [choices[0].Id] = PollAnswer.Yes, [choices[1].Id] = PollAnswer.Yes }));
            Assert.Throws<InvalidOperationException>(() => manager.Vote(schedule, AliceId, new Dictionary<Guid, PollAnswer> { [dates[0].Id] = PollAnswer.Maybe }));
            Assert.Throws<InvalidOperationException>(() => manager.Vote(expired, AliceId, new Dictionary<Guid, PollAnswer> { [late[0].Id] = PollAnswer.Yes }));

            manager.Close(decision, null, AliceId);

            Assert.Throws<InvalidOperationException>(() => manager.Vote(decision, BobId, new Dictionary<Guid, PollAnswer> { [choices[0].Id] = PollAnswer.Yes }));
            Assert.Empty(manager.GetVotes(decision));
            Assert.Empty(manager.GetVotes(schedule));
        }

        /// <summary>
        /// A decision allowing several choices takes them.
        /// </summary>
        [Fact]
        public void MultipleChoiceTakesSeveral()
        {
            var database = nameof(MultipleChoiceTakesSeveral);
            Seed(database);

            var (id, options) = Create(database, DecisionPollType.TypeKey, WorkspaceId, "Which?", 3, x => x.MultipleChoice = true);

            PollHub.PollManager.Vote(id, AliceId, new Dictionary<Guid, PollAnswer> { [options[0].Id] = PollAnswer.Yes, [options[2].Id] = PollAnswer.Yes });

            var result = PollHub.PollManager.GetResult(id);

            Assert.Single(result.Voters);
            Assert.Equal(2, result.Leaders.Count);
        }

        /// <summary>
        /// An option keeping its id keeps its votes; a removed option takes its votes and its
        /// choice as the poll's result with it.
        /// </summary>
        [Fact]
        public void SetOptionsKeepsTheVotesOfUnchangedOptions()
        {
            var database = nameof(SetOptionsKeepsTheVotesOfUnchangedOptions);
            Seed(database);

            var (id, options) = Create(database, DecisionPollType.TypeKey, WorkspaceId, "Which?", 3);
            var manager = PollHub.PollManager;

            manager.Vote(id, AliceId, new Dictionary<Guid, PollAnswer> { [options[0].Id] = PollAnswer.Yes });
            manager.Vote(id, BobId, new Dictionary<Guid, PollAnswer> { [options[2].Id] = PollAnswer.Yes });
            manager.Close(id, options[2].Id, AliceId);

            var stored = manager.SetOptions(id,
            [
                new PollOption { Id = options[0].Id, Text = options[0].Text },
                new PollOption { Text = "Option D" }
            ]);

            Assert.Equal(2, stored.Count);
            Assert.Equal(options[0].Id, stored[0].Id);
            Assert.Equal(AliceId, Assert.Single(manager.GetVotes(id)).IdentityId);
            Assert.Null(manager.GetPoll(id).ChosenOptionId);
        }

        /// <summary>
        /// Changing the type of a poll removes its votes; an unknown type is refused.
        /// </summary>
        [Fact]
        public void TypeChangeClearsTheVotes()
        {
            var database = nameof(TypeChangeClearsTheVotes);
            Seed(database);

            var (id, options) = Create(database, DecisionPollType.TypeKey, WorkspaceId, "Which?", 2);
            var manager = PollHub.PollManager;

            manager.Vote(id, AliceId, new Dictionary<Guid, PollAnswer> { [options[0].Id] = PollAnswer.Yes });

            var poll = manager.GetPoll(id);
            poll.Type = "SCHEDULE";
            manager.SavePoll(poll);

            Assert.Equal(SchedulePollType.TypeKey, manager.GetPoll(id).Type);
            Assert.Empty(manager.GetVotes(id));

            poll.Type = "lottery";
            Assert.Throws<InvalidOperationException>(() => manager.SavePoll(poll));
        }

        /// <summary>
        /// Closing settles on the single leader unless told otherwise; a tie settles on nothing;
        /// opening again forgets the result and a deadline that has passed.
        /// </summary>
        [Fact]
        public void CloseSettlesAndReopenForgets()
        {
            var database = nameof(CloseSettlesAndReopenForgets);
            Seed(database);

            var (id, options) = Create(database, DecisionPollType.TypeKey, WorkspaceId, "Which?", 2);
            var (tie, tied) = Create(database, DecisionPollType.TypeKey, WorkspaceId, "Tie?", 2);
            var (plain, _) = Create(database, DecisionPollType.TypeKey, WorkspaceId, "Plain?", 2);
            var manager = PollHub.PollManager;

            manager.Vote(id, AliceId, new Dictionary<Guid, PollAnswer> { [options[1].Id] = PollAnswer.Yes });
            manager.Vote(tie, AliceId, new Dictionary<Guid, PollAnswer> { [tied[0].Id] = PollAnswer.Yes });
            manager.Vote(tie, BobId, new Dictionary<Guid, PollAnswer> { [tied[1].Id] = PollAnswer.Yes });
            manager.Vote(plain, AliceId, new Dictionary<Guid, PollAnswer> { [manager.GetOptions(plain)[0].Id] = PollAnswer.Yes });

            var closed = manager.Close(id, null, BobId);
            Assert.Equal(options[1].Id, closed.ChosenOptionId);
            Assert.Equal(BobId, closed.ClosedById);
            Assert.False(manager.IsRunning(closed, DateTime.UtcNow));

            Assert.Null(manager.Close(tie, null, BobId).ChosenOptionId);
            Assert.Null(manager.Close(plain, null, BobId, takeLeader: false).ChosenOptionId);
            Assert.Throws<InvalidOperationException>(() => manager.Close(id, tied[0].Id, BobId));

            var poll = manager.GetPoll(id);
            poll.Deadline = DateTime.UtcNow.AddDays(-1);
            Model.ModelHub.SavePoll(poll);

            var reopened = manager.Reopen(id, BobId);

            Assert.Null(reopened.Closed);
            Assert.Null(reopened.ClosedById);
            Assert.Null(reopened.ChosenOptionId);
            Assert.Null(reopened.Deadline);
            Assert.True(manager.IsRunning(reopened, DateTime.UtcNow));
            Assert.Single(manager.GetVotes(id));
        }

        /// <summary>
        /// The polls whose deadline ran out are closed by the system, once.
        /// </summary>
        [Fact]
        public void CloseExpiredClosesWhatRanOut()
        {
            var database = nameof(CloseExpiredClosesWhatRanOut);
            Seed(database);

            var now = DateTime.UtcNow;
            var (expired, _) = Create(database, DecisionPollType.TypeKey, WorkspaceId, "Expired", 2, x => x.Deadline = now.AddMinutes(-5));
            var (later, _) = Create(database, DecisionPollType.TypeKey, WorkspaceId, "Later", 2, x => x.Deadline = now.AddDays(1));
            var manager = PollHub.PollManager;

            Assert.Equal(1, manager.CloseExpired(now));
            Assert.Equal(0, manager.CloseExpired(now));

            var closed = manager.GetPoll(expired);
            Assert.NotNull(closed.Closed);
            Assert.Null(closed.ClosedById);
            Assert.Null(manager.GetPoll(later).Closed);
        }

        /// <summary>
        /// The running polls of a workspace leave out closed and expired polls and those of
        /// other workspaces, list the next deadline first and say whether the reader voted.
        /// </summary>
        [Fact]
        public void GetRunningListsTheOpenPollsOfTheWorkspace()
        {
            var database = nameof(GetRunningListsTheOpenPollsOfTheWorkspace);
            Seed(database);

            var now = DateTime.UtcNow;
            var (open, openOptions) = Create(database, DecisionPollType.TypeKey, WorkspaceId, "Open", 2);
            var (soon, _) = Create(database, SchedulePollType.TypeKey, WorkspaceId, "Soon", 2, x => x.Deadline = now.AddHours(2));
            var (closed, _) = Create(database, DecisionPollType.TypeKey, WorkspaceId, "Closed", 2);
            Create(database, DecisionPollType.TypeKey, WorkspaceId, "Expired", 2, x => x.Deadline = now.AddHours(-2));
            Create(database, DecisionPollType.TypeKey, OtherWorkspaceId, "Elsewhere", 2);

            var manager = PollHub.PollManager;
            manager.Close(closed, null, AliceId);
            manager.Vote(open, AliceId, new Dictionary<Guid, PollAnswer> { [openOptions[0].Id] = PollAnswer.Yes });

            var running = manager.GetRunning(Team(database), AliceId, now, 10);

            Assert.Equal([soon, open], running.Select(x => x.Object.Id));
            Assert.False(running[0].Voted);
            Assert.True(running[1].Voted);
            Assert.Single(manager.GetRunning(Team(database), AliceId, now, 1));
        }
    }
}
