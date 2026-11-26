using KleeneStar.Core;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Linq;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Poll.WebPoll
{
    /// <summary>
    /// Creates the object of a new poll the way the core's object endpoint creates any object:
    /// one genesis commit holding the object row and the entry state of every workflow of its
    /// class.
    /// </summary>
    public static class PollObjects
    {
        /// <summary>
        /// Creates a poll object.
        /// </summary>
        /// <param name="class">The poll class.</param>
        /// <param name="summary">The question.</param>
        /// <param name="description">The description (an editor document), or <see langword="null"/>.</param>
        /// <param name="creatorId">Who creates it, or <see cref="Guid.Empty"/>.</param>
        /// <returns>The object.</returns>
        public static ObjectEntity Create(Class @class, string summary, string description, Guid creatorId)
        {
            ArgumentNullException.ThrowIfNull(@class);

            var actor = creatorId == Guid.Empty ? (Guid?)null : creatorId;
            var entity = new ObjectEntity(Guid.NewGuid())
            {
                Key = CoreHub.ObjectManager.NextObjectKey(@class.WorkspaceId),
                Summary = string.IsNullOrWhiteSpace(summary) ? "?" : summary.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description,
                Icon = @class.Icon,
                State = WorkspaceState.Active,
                Kind = ObjectKind.Poll,
                WorkspaceId = @class.WorkspaceId,
                ClassId = @class.Id,
                SecurityLevelId = CoreHub.SecurityLevelManager?.GetDefaultSecurityLevel(@class.Id)?.Id,
                CreatorId = actor,
                UpdaterId = actor,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            };

            using (CoreHub.CommitManager.BeginCommit(entity.Id, CommitType.Created, creatorId))
            {
                CoreHub.ObjectManager.Add(entity);

                StartWorkflows(entity);
            }

            return entity;
        }

        /// <summary>
        /// Places every workflow field of the object that holds no state on the first entry state
        /// of its workflow - what the core's object endpoint does for every new object.
        /// </summary>
        /// <param name="entity">The object.</param>
        public static void StartWorkflows(ObjectEntity entity)
        {
            var fields = CoreHub.FieldManager
                .GetFields(new ClassIdParameter(entity.ClassId))
                .Where(f => !f.Deprecated && f.State == FieldState.Active && f.FieldType == FieldType.Workflow && f.WorkflowId.HasValue);

            foreach (var field in fields)
            {
                var workflow = CoreHub.WorkflowManager.GetWorkflowWithStructure(field.WorkflowId.Value);
                var entry = workflow is null ? null : CoreHub.WorkflowManager.GetTargetStatuses(workflow, null).FirstOrDefault();

                if (entry is null)
                {
                    continue;
                }

                var existing = CoreHub.ValueManager.GetValue(entity.Id, field.Id);

                if (existing is null)
                {
                    CoreHub.ValueManager.Add(new Value
                    {
                        ObjectId = entity.Id,
                        FieldId = field.Id,
                        Data = entry.Name,
                        Created = DateTime.UtcNow,
                        Updated = DateTime.UtcNow
                    });
                }
                else if (string.IsNullOrWhiteSpace(existing.Data))
                {
                    existing.Data = entry.Name;
                    existing.Updated = DateTime.UtcNow;
                    CoreHub.ValueManager.Update(existing);
                }
            }
        }
    }
}
