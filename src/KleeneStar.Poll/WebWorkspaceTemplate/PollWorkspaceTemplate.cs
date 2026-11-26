using KleeneStar.Core.WebWorkspaceTemplate;
using KleeneStar.Model.Entities;
using System.Collections.Generic;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Poll.WebWorkspaceTemplate
{
    /// <summary>
    /// The workspace template of team polls: one poll class, <c>Poll</c>, whose workflow knows
    /// the states a poll passes - open, decided, cancelled - so that closing a poll moves it into
    /// <c>Decided</c>.
    /// </summary>
    /// <remarks>
    /// An existing workspace gets polls by a class of the poll kind; the template is the shortcut
    /// for a workspace that is mainly one. The texts are internationalization keys, resolved once
    /// in the language of whoever creates the workspace.
    /// </remarks>
    public sealed class PollWorkspaceTemplate : IWorkspaceTemplate
    {
        /// <summary>
        /// The address of the icon of the template and its class.
        /// </summary>
        public const string IconPath = "/kleenestar/kleenestar.poll/assets/img/kleenestar.poll.svg";

        /// <summary>
        /// Gets the stable key of the template.
        /// </summary>
        public string Key => "kleenestar.poll.teampolls";

        /// <summary>
        /// Gets the internationalization key of the display name.
        /// </summary>
        public string Name => "kleenestar.poll:template.teampolls.name";

        /// <summary>
        /// Gets the internationalization key of the description.
        /// </summary>
        public string Description => "kleenestar.poll:template.teampolls.description";

        /// <summary>
        /// Gets the icon of the template.
        /// </summary>
        public IIcon Icon => ImageIcon.FromString(IconPath);

        /// <summary>
        /// Gets the suggested key of the workspace.
        /// </summary>
        public string SuggestedKey => "VOTE";

        /// <summary>
        /// Gets the categories the workspace is filed under.
        /// </summary>
        public IEnumerable<string> Categories => [];

        /// <summary>
        /// Gets the display order among the templates offered; after the team calendar (150).
        /// </summary>
        public int Order => 160;

        /// <summary>
        /// Gets the classes the workspace starts with.
        /// </summary>
        public IEnumerable<WorkspaceTemplateClass> Classes =>
        [
            new WorkspaceTemplateClass
            {
                Name = "Poll",
                Description = "kleenestar.poll:template.teampolls.class.poll",
                Icon = IconPath,
                Kind = ObjectKind.Poll,
                Fields =
                [
                    new WorkspaceTemplateField
                    {
                        Name = "Description",
                        Description = "kleenestar.poll:template.field.description",
                        Type = FieldType.Text
                    },
                    new WorkspaceTemplateField
                    {
                        Name = "Status",
                        Description = "kleenestar.poll:template.field.status",
                        Type = FieldType.Workflow
                    },
                    new WorkspaceTemplateField
                    {
                        Name = "Labels",
                        Description = "kleenestar.poll:template.field.labels",
                        Type = FieldType.Tag,
                        Tab = WorkspaceTemplateField.DetailsTab
                    }
                ],
                Workflow = Workflow()
            }
        ];

        /// <summary>
        /// Declares the workflow of a poll.
        /// </summary>
        /// <returns>The workflow.</returns>
        public static WorkspaceTemplateWorkflow Workflow()
        {
            const string open = "kleenestar.poll:template.status.open";
            const string decided = "kleenestar.poll:template.status.decided";
            const string cancelled = "kleenestar.poll:template.status.cancelled";

            return new WorkspaceTemplateWorkflow
            {
                Name = "kleenestar.poll:template.workflow.name",
                Description = "kleenestar.poll:template.workflow.description",
                Statuses =
                [
                    new WorkspaceTemplateStatus { Name = open, Description = "kleenestar.poll:template.status.open.description", Category = WorkspaceTemplateStatus.InProgress, Icon = "/kleenestar/assets/icons/state-progress.svg" },

                    // the closing state reached by closing a poll comes first among the closing
                    // ones, so the poll closes into it
                    new WorkspaceTemplateStatus { Name = decided, Description = "kleenestar.poll:template.status.decided.description", Category = WorkspaceTemplateStatus.Done, IsEnd = true, Icon = "/kleenestar/assets/icons/state-resolved.svg" },
                    new WorkspaceTemplateStatus { Name = cancelled, Description = "kleenestar.poll:template.status.cancelled.description", Category = WorkspaceTemplateStatus.Done, IsEnd = true, Icon = "/kleenestar/assets/icons/state-closed.svg" }
                ],
                Transitions =
                [
                    new WorkspaceTemplateTransition { Name = "kleenestar.poll:template.transition.decide", From = open, To = decided },
                    new WorkspaceTemplateTransition { Name = "kleenestar.poll:template.transition.cancel", From = open, To = cancelled },
                    new WorkspaceTemplateTransition { Name = "kleenestar.poll:template.transition.reopen", From = decided, To = open },
                    new WorkspaceTemplateTransition { Name = "kleenestar.poll:template.transition.reopen", From = cancelled, To = open }
                ]
            };
        }
    }
}
