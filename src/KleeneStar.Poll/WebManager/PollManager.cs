using KleeneStar.Core;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using KleeneStar.Poll.WebPoll;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;
using ObjectEntity = KleeneStar.Model.Entities.Object;
using PollEntity = KleeneStar.Model.Entities.Poll;

namespace KleeneStar.Poll.WebManager
{
    /// <summary>
    /// The implementation of <see cref="IPollManager"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The settings, options and votes are read from the model directly, because they carry no
    /// visibility of their own; the objects they belong to are always read through the core's
    /// object manager, which narrows to what the caller may see.
    /// </para>
    /// <para>
    /// A poll is closed in two places that are kept in step: its own <c>Closed</c> mark, which
    /// decides whether it takes votes, and its workflow. Closing a poll moves the workflow into
    /// the first closing state it leads to; a person moving the workflow into a closing state
    /// (or out of one) closes (or opens) the poll. Closing at the deadline is a transition
    /// without actor: it is the system that closes it, not a person.
    /// </para>
    /// </remarks>
    public sealed class PollManager : IPollManager
    {
        private readonly IHttpServerContext _httpServerContext;
        private readonly object _gate = new();
        private readonly AsyncLocal<bool> _transitioning = new();
        private bool _connected;

        /// <inheritdoc/>
        public event EventHandler<PollEntity> PollChanged;

        /// <inheritdoc/>
        public event EventHandler<Guid> OptionsChanged;

        /// <inheritdoc/>
        public event EventHandler<PollBallot> Voted;

        /// <inheritdoc/>
        public event EventHandler<PollEntity> PollClosed;

        /// <inheritdoc/>
        public event EventHandler<PollEntity> PollReopened;

        /// <summary>
        /// Initializes a new instance of the class. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The server context, for the log.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private PollManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _httpServerContext = httpServerContext;
        }

        /// <inheritdoc/>
        public void Connect()
        {
            lock (_gate)
            {
                if (_connected)
                {
                    return;
                }

                _connected = true;
            }

            CoreHub.ObjectManager.ObjectAdded += (_, entity) => Capture(() => OnObjectAdded(entity));
            CoreHub.WorkflowManager.TransitionExecuted += (_, result) => Capture(() => OnTransition(result));

            ConnectAudit();
        }

        /// <inheritdoc/>
        public TimeZoneInfo GetDisplayZone(Guid identityId)
        {
            var identity = identityId == Guid.Empty ? null : CoreHub.IdentityManager?.GetIdentity(identityId);

            return PollTime.Zone(!string.IsNullOrWhiteSpace(identity?.TimeZone) ? identity.TimeZone : PollHub.Settings.TimeZone);
        }

        /// <inheritdoc/>
        public PollEntity GetPoll(Guid objectId)
        {
            return objectId == Guid.Empty ? null : ModelHub.GetPoll(objectId);
        }

        /// <inheritdoc/>
        public IReadOnlyDictionary<Guid, PollEntity> GetPolls(IEnumerable<Guid> objectIds)
        {
            return ModelHub.GetPolls(objectIds);
        }

        /// <inheritdoc/>
        public PollEntity SavePoll(PollEntity poll)
        {
            ArgumentNullException.ThrowIfNull(poll);

            if (poll.ObjectId == Guid.Empty)
            {
                throw new InvalidOperationException("A poll belongs to a poll object.");
            }

            var type = PollHub.PollTypeManager.GetType(poll.Type)
                ?? throw new InvalidOperationException($"The poll type '{poll.Type}' is unknown.");

            poll.Type = type.Key;
            poll.Deadline = poll.Deadline is DateTime deadline ? DateTime.SpecifyKind(deadline, DateTimeKind.Utc) : null;

            var stored = ModelHub.GetPoll(poll.ObjectId);

            // the closing is Close's and Reopen's business
            if (stored is not null)
            {
                poll.Closed = stored.Closed;
                poll.ClosedById = stored.ClosedById;
                poll.ChosenOptionId = stored.ChosenOptionId;
            }

            ModelHub.SavePoll(poll);

            // a date answered is no choice made: what was voted on is gone
            if (stored is not null && !string.Equals(stored.Type, poll.Type, StringComparison.OrdinalIgnoreCase))
            {
                ModelHub.ClearPollVotes(poll.ObjectId);
            }

            PollChanged?.Invoke(this, poll);

            return poll;
        }

        /// <inheritdoc/>
        public IReadOnlyList<PollOption> GetOptions(Guid objectId)
        {
            return objectId == Guid.Empty ? [] : ModelHub.GetPollOptions(objectId);
        }

        /// <inheritdoc/>
        public IReadOnlyList<PollOption> SetOptions(Guid objectId, IEnumerable<PollOption> options)
        {
            if (objectId == Guid.Empty)
            {
                throw new InvalidOperationException("Options belong to a poll object.");
            }

            var list = (options ?? []).Where(x => x is not null).ToList();

            foreach (var option in list)
            {
                option.Text = string.IsNullOrWhiteSpace(option.Text) ? null : option.Text.Trim();
                option.Start = option.Start is DateTime start ? DateTime.SpecifyKind(start, DateTimeKind.Utc) : null;
                option.End = option.End is DateTime end ? DateTime.SpecifyKind(end, DateTimeKind.Utc) : null;

                if (option.Start is null && option.Text is null)
                {
                    throw new InvalidOperationException("An option needs a text or a date.");
                }

                if (option.Start is not null && option.End is not null && option.End < option.Start)
                {
                    throw new InvalidOperationException("An option cannot end before it starts.");
                }
            }

            var stored = ModelHub.SetPollOptions(objectId, list);

            // a chosen option that was removed is no longer chosen
            var poll = ModelHub.GetPoll(objectId);

            if (poll?.ChosenOptionId is Guid chosen && !stored.Any(x => x.Id == chosen))
            {
                poll.ChosenOptionId = null;
                ModelHub.SavePoll(poll);
            }

            OptionsChanged?.Invoke(this, objectId);

            return stored;
        }

        /// <inheritdoc/>
        public IReadOnlyList<PollVote> GetVotes(Guid objectId)
        {
            return objectId == Guid.Empty ? [] : ModelHub.GetPollVotes(objectId);
        }

        /// <inheritdoc/>
        public IReadOnlyDictionary<Guid, int> GetVoterCounts(IEnumerable<Guid> objectIds)
        {
            return ModelHub.GetPollVoterCounts(objectIds);
        }

        /// <inheritdoc/>
        public IReadOnlySet<Guid> GetVotedBy(IEnumerable<Guid> objectIds, Guid identityId)
        {
            return ModelHub.GetPollsVotedBy(objectIds, identityId);
        }

        /// <inheritdoc/>
        public PollResult GetResult(Guid objectId)
        {
            var poll = GetPoll(objectId);

            if (poll is null)
            {
                return null;
            }

            return PollResult.Tally(poll, PollHub.PollTypeManager.Resolve(poll.Type), GetOptions(objectId), GetVotes(objectId));
        }

        /// <inheritdoc/>
        public bool IsRunning(PollEntity poll, DateTime now)
        {
            return poll is not null
                && poll.Closed is null
                && (poll.Deadline is null || poll.Deadline > now);
        }

        /// <inheritdoc/>
        public IReadOnlyList<PollVote> Vote(Guid objectId, Guid identityId, IReadOnlyDictionary<Guid, PollAnswer> answers)
        {
            if (identityId == Guid.Empty)
            {
                throw new InvalidOperationException("Nobody cannot vote.");
            }

            var poll = GetPoll(objectId)
                ?? throw new InvalidOperationException("The object is no poll.");

            if (!IsRunning(poll, DateTime.UtcNow))
            {
                throw new InvalidOperationException("The poll is closed.");
            }

            var type = PollHub.PollTypeManager.Resolve(poll.Type);
            var options = GetOptions(objectId).Select(x => x.Id).ToHashSet();
            var wanted = new Dictionary<Guid, PollAnswer>();

            foreach (var (optionId, answer) in answers ?? new Dictionary<Guid, PollAnswer>())
            {
                if (!options.Contains(optionId))
                {
                    throw new InvalidOperationException("The option belongs to another poll.");
                }

                if (!PollResult.Allowed(poll, type, answer))
                {
                    throw new InvalidOperationException($"The poll does not allow the answer '{answer}'.");
                }

                wanted[optionId] = answer;
            }

            if (!type.Graded && !poll.MultipleChoice && wanted.Count > 1)
            {
                throw new InvalidOperationException("The poll allows one choice.");
            }

            var stored = ModelHub.SetPollVotes(objectId, identityId, wanted);

            Voted?.Invoke(this, new PollBallot { ObjectId = objectId, IdentityId = identityId, Answers = stored.Count });

            return stored;
        }

        /// <inheritdoc/>
        public PollEntity Close(Guid objectId, Guid? chosenOptionId, Guid actorId, bool takeLeader = true)
        {
            var poll = GetPoll(objectId);

            if (poll is null)
            {
                return null;
            }

            if (chosenOptionId is Guid chosen && !GetOptions(objectId).Any(x => x.Id == chosen))
            {
                throw new InvalidOperationException("The option belongs to another poll.");
            }

            var wasClosed = poll.Closed is not null;

            poll.ChosenOptionId = chosenOptionId ?? (takeLeader ? (wasClosed ? poll.ChosenOptionId : SingleLeader(poll)) : null);
            poll.Closed ??= DateTime.UtcNow;
            poll.ClosedById = wasClosed ? poll.ClosedById : (actorId == Guid.Empty ? null : actorId);

            ModelHub.SavePoll(poll);

            if (!wasClosed)
            {
                Transition(objectId, done: true, actorId);
                PollClosed?.Invoke(this, poll);
            }
            else
            {
                PollChanged?.Invoke(this, poll);
            }

            return poll;
        }

        /// <inheritdoc/>
        public PollEntity Reopen(Guid objectId, Guid actorId)
        {
            var poll = GetPoll(objectId);

            if (poll is null || poll.Closed is null)
            {
                return poll;
            }

            poll.Closed = null;
            poll.ClosedById = null;
            poll.ChosenOptionId = null;

            if (poll.Deadline is DateTime deadline && deadline <= DateTime.UtcNow)
            {
                poll.Deadline = null;
            }

            ModelHub.SavePoll(poll);

            Transition(objectId, done: false, actorId);
            PollReopened?.Invoke(this, poll);

            return poll;
        }

        /// <inheritdoc/>
        public IReadOnlyList<RunningPoll> GetRunning(Workspace workspace, Guid identityId, DateTime now, int limit)
        {
            if (workspace is null)
            {
                return [];
            }

            var polls = ModelHub.GetRunningPolls([workspace.Id], now);

            if (polls.Count == 0)
            {
                return [];
            }

            var ids = polls.Select(x => x.ObjectId).ToList();
            var objects = CoreHub.ObjectManager
                .GetObjects(new Query<ObjectEntity>().Where(x => ids.Contains(x.Id)))
                .Where(x => x.State == WorkspaceState.Active)
                .GroupBy(x => x.Id)
                .ToDictionary(x => x.Key, x => x.First());

            var categories = Categories(objects.Values);
            var voted = ModelHub.GetPollsVotedBy(objects.Keys, identityId);

            return [.. polls
                .Where(x => objects.ContainsKey(x.ObjectId))
                .Where(x => !IsDone(categories.GetValueOrDefault(x.ObjectId)))
                .OrderBy(x => x.Deadline ?? DateTime.MaxValue)
                .ThenByDescending(x => objects[x.ObjectId].Created)
                .Take(Math.Max(1, limit))
                .Select(x => new RunningPoll
                {
                    Object = objects[x.ObjectId],
                    Poll = x,
                    Voted = voted.Contains(x.ObjectId)
                })];
        }

        /// <inheritdoc/>
        public int CloseExpired(DateTime now)
        {
            var closed = 0;

            // the closing is the system's own business, and a poll classified above the reach of
            // anybody would otherwise never be closed
            using var unrestricted = CoreHub.SecurityLevelManager?.BeginUnrestricted();

            foreach (var poll in ModelHub.GetPollsToClose(now, 200))
            {
                try
                {
                    Close(poll.ObjectId, null, Guid.Empty);
                    closed++;
                }
                catch (Exception ex)
                {
                    _httpServerContext?.Log?.Exception(ex);
                }
            }

            return closed;
        }

        /// <summary>
        /// Answers the only leading option of a poll.
        /// </summary>
        /// <param name="poll">The settings of the poll.</param>
        /// <returns>The option, or <see langword="null"/> without a single leader.</returns>
        private Guid? SingleLeader(PollEntity poll)
        {
            var leaders = PollResult.Tally(poll, PollHub.PollTypeManager.Resolve(poll.Type), GetOptions(poll.ObjectId), GetVotes(poll.ObjectId)).Leaders;

            return leaders.Count == 1 ? leaders[0].Option.Id : null;
        }

        /// <summary>
        /// Moves a poll into the first closing state its workflow leads to, or out of a closing
        /// state into the first open one.
        /// </summary>
        /// <param name="objectId">The poll object.</param>
        /// <param name="done">Whether the poll is to reach a closing state.</param>
        /// <param name="actorId">Who moves it; <see cref="Guid.Empty"/> for the system.</param>
        /// <returns><see langword="true"/> when the poll was moved.</returns>
        private bool Transition(Guid objectId, bool done, Guid actorId)
        {
            ObjectEntity entity;

            // the poll was read with the caller's visibility by whoever asked to close it; the
            // move of its state follows from that and must not fail on a security level
            using (CoreHub.SecurityLevelManager?.BeginUnrestricted())
            {
                entity = CoreHub.ObjectManager.GetObject(objectId);
            }

            if (entity is null || entity.State != WorkspaceState.Active)
            {
                return false;
            }

            var field = CoreHub.FieldManager
                .GetFields(new ClassIdParameter(entity.ClassId))
                .FirstOrDefault(x => !x.Deprecated && x.State == FieldState.Active && x.FieldType == FieldType.Workflow && x.WorkflowId is not null);

            var workflow = field is null ? null : CoreHub.WorkflowManager.GetWorkflowWithStructure(field.WorkflowId.Value);

            if (workflow is null)
            {
                return false;
            }

            var current = CoreHub.WorkflowManager.ResolveStatus(workflow, CoreHub.ValueManager.GetValue(entity.Id, field.Id)?.Data);

            if (IsDone(Category(current)) == done)
            {
                return false;
            }

            // the first state of the wanted side the workflow leads to from here - the order of
            // the states is the template's, which puts "decided" before "cancelled"
            var target = (current is null ? workflow.Statuses : CoreHub.WorkflowManager.GetTargetStatuses(workflow, current))?
                .Where(x => IsDone(Category(x)) == done)
                .FirstOrDefault();

            if (target is null)
            {
                return false;
            }

            _transitioning.Value = true;

            try
            {
                var result = CoreHub.WorkflowManager.ExecuteTransition(entity.Id, field.Id, target.Id, actorId);

                return result?.Outcome == WorkflowTransitionOutcome.Executed;
            }
            finally
            {
                _transitioning.Value = false;
            }
        }

        /// <summary>
        /// Keeps a poll in step with its workflow when a person moves it: into a closing state
        /// closes it, out of one opens it again.
        /// </summary>
        /// <param name="result">The transition executed.</param>
        private void OnTransition(WorkflowTransitionResult result)
        {
            if (_transitioning.Value || result?.Outcome != WorkflowTransitionOutcome.Executed || result.Target is null)
            {
                return;
            }

            var poll = ModelHub.GetPoll(result.ObjectId);

            if (poll is null)
            {
                return;
            }

            var done = IsDone(Category(result.Target));

            if (done && poll.Closed is null)
            {
                poll.ChosenOptionId ??= SingleLeader(poll);
                poll.Closed = DateTime.UtcNow;
                poll.ClosedById = result.IdentityId == Guid.Empty ? null : result.IdentityId;
                ModelHub.SavePoll(poll);
                PollClosed?.Invoke(this, poll);
            }
            else if (!done && poll.Closed is not null)
            {
                poll.Closed = null;
                poll.ClosedById = null;
                poll.ChosenOptionId = null;

                if (poll.Deadline is DateTime deadline && deadline <= DateTime.UtcNow)
                {
                    poll.Deadline = null;
                }

                ModelHub.SavePoll(poll);
                PollReopened?.Invoke(this, poll);
            }
        }

        /// <summary>
        /// Gives a poll created without settings - through the object wizard, an automation or a
        /// clone - its settings: a decision without options, which its page offers to complete.
        /// A template may preset the type (<c>kleenestar.poll:type</c>).
        /// </summary>
        /// <param name="entity">The object added.</param>
        private void OnObjectAdded(ObjectEntity entity)
        {
            if (entity is null || !string.Equals(entity.Kind, ObjectKind.Poll, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (ModelHub.GetPoll(entity.Id) is not null)
            {
                return;
            }

            var creator = entity.CreatorId is Guid id && id != Guid.Empty ? id : Guid.Empty;

            ModelHub.SavePoll(new PollEntity
            {
                ObjectId = entity.Id,
                Type = DecisionPollType.TypeKey,
                TimeZone = GetDisplayZone(creator).Id
            });
        }

        /// <summary>
        /// Resolves the status categories of objects from their workflow fields.
        /// </summary>
        /// <param name="objects">The objects.</param>
        /// <returns>The categories, keyed by object; an object without workflow is missing.</returns>
        private static IReadOnlyDictionary<Guid, StatusCategory> Categories(IEnumerable<ObjectEntity> objects)
        {
            var list = objects.ToList();

            if (list.Count == 0)
            {
                return new Dictionary<Guid, StatusCategory>();
            }

            var contexts = new Dictionary<Guid, ObjectBoardClassContext>();

            foreach (var classId in list.Select(x => x.ClassId).Distinct())
            {
                var @class = CoreHub.ClassManager.GetClass(classId);
                contexts[classId] = @class is null ? null : ObjectBoardProjection.BuildClassContext(@class);
            }

            var read = ObjectBoardProjection.ReadValues(list, contexts.Values.Where(x => x is not null));
            var categoriesById = ObjectBoardProjection.GetOrderedCategories().ToDictionary(x => x.Id);
            var result = new Dictionary<Guid, StatusCategory>();

            foreach (var entity in list)
            {
                var context = contexts[entity.ClassId];

                if (context?.WorkflowField is null)
                {
                    continue;
                }

                var category = ObjectBoardProjection.ResolveCategory(read(entity.Id, context.WorkflowField), context, categoriesById);

                if (category is not null)
                {
                    result[entity.Id] = category;
                }
            }

            return result;
        }

        /// <summary>
        /// Answers the category of a status.
        /// </summary>
        /// <param name="status">The status, or <see langword="null"/>.</param>
        /// <returns>The category, or <see langword="null"/>.</returns>
        private static StatusCategory Category(Status status)
        {
            return status is null
                ? null
                : ObjectBoardProjection.GetOrderedCategories().FirstOrDefault(x => x.Id == status.CategoryId);
        }

        /// <summary>
        /// Determines whether a status category closes an object.
        /// </summary>
        /// <param name="category">The category, or <see langword="null"/>.</param>
        /// <returns><see langword="true"/> for a closing category.</returns>
        public static bool IsDone(StatusCategory category)
        {
            return category is not null && ObjectBoardProjection.CategoryProgress(category) >= 100;
        }

        /// <summary>
        /// Records the changes of the plugin in the audit log - what the core's
        /// <c>AuditManager.Connect</c> does for its own managers, here because the core cannot
        /// name this one.
        /// </summary>
        private void ConnectAudit()
        {
            void Record(AuditCategory category, AuditAction action, object entity, AuditSeverity severity)
            {
                Capture(() => CoreHub.AuditManager?.RecordChange(category, action, entity, severity));
            }

            PollChanged += (_, x) => Record(AuditCategory.Content, AuditAction.Updated, x, AuditSeverity.Info);
            Voted += (_, x) => Record(AuditCategory.Content, AuditAction.Updated, x, AuditSeverity.Info);
            PollClosed += (_, x) => Record(AuditCategory.Workflow, AuditAction.Updated, x, AuditSeverity.Info);
            PollReopened += (_, x) => Record(AuditCategory.Workflow, AuditAction.Updated, x, AuditSeverity.Info);
        }

        /// <summary>
        /// Releases nothing: the manager holds no resources of its own; the subscriptions live as
        /// long as the core's managers do.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Runs a reaction without letting a failure reach the write that raised the event.
        /// </summary>
        /// <param name="action">The reaction.</param>
        private void Capture(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _httpServerContext?.Log?.Exception(ex);
            }
        }
    }
}
