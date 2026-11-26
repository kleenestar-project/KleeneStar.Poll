using KleeneStar.Core;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using KleeneStar.Poll.WebPoll;
using System.Collections.Generic;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Poll.WebFragment.Poll
{
    /// <summary>
    /// Builds the inputs of the poll dialog, shared by the dialog creating a poll and the one
    /// changing it: the question and its options on the first tab, the settings on the second.
    /// </summary>
    public static class PollFormItems
    {
        /// <summary>
        /// Builds the inputs.
        /// </summary>
        /// <param name="workspace">The workspace the poll belongs to.</param>
        /// <param name="poll">The poll being changed, or <see langword="null"/> for a new one.</param>
        /// <returns>The inputs.</returns>
        public static IEnumerable<ControlFormItem> Build(Workspace workspace, Model.Entities.Object poll)
        {
            var general = new ControlFormItemGroupTabView("poll-tab-general")
            {
                Title = _ => "kleenestar.poll:poll.tab.general",
                Icon = _ => new IconSquarePollVertical()
            };

            var types = new ControlFormItemInputSelection
            {
                Name = _ => PollForm.Type,
                Label = _ => "kleenestar.poll:poll.type.label",
                Help = _ => poll is null ? "kleenestar.poll:poll.type.help" : "kleenestar.poll:poll.type.help.change",
                StickySelection = _ => true,
                Required = _ => true
            };

            foreach (var type in PollHub.PollTypeManager.Types)
            {
                types.Add(new ControlFormItemInputSelectionItem(type.Key)
                {
                    Text = _ => type.Label,
                    Icon = _ => type.Icon
                });
            }

            if (poll is null)
            {
                general.Add(new ControlFormItemGroupColumnHorizontal
                (
                    types,
                    new ControlDataFormItemInputSelection
                    {
                        Name = _ => PollForm.ClassId,
                        Label = _ => "kleenestar.poll:poll.class.label",
                        Help = _ => "kleenestar.poll:poll.class.help",
                        StickySelection = _ => true,
                        ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub
                            .GetUri<global::KleeneStar.Poll.WWW.Api._1_.Polls._workspacekey_.Classes>()?
                            .BindParameters(new WorkspaceKeyParameter(workspace?.Key))?
                            .ToString())
                    }
                ));
            }
            else
            {
                general.Add(types);
            }

            general.Add(new ControlFormItemInputText
            {
                Name = _ => PollForm.Summary,
                Label = _ => "kleenestar.poll:poll.summary.label",
                Placeholder = _ => "kleenestar.poll:poll.summary.placeholder",
                Required = _ => true
            });

            general.Add(new ControlFormItemInputText
            {
                Name = _ => PollForm.Options,
                Label = _ => "kleenestar.poll:poll.options.label",
                Help = _ => "kleenestar.poll:poll.options.help",
                Placeholder = _ => "kleenestar.poll:poll.options.placeholder",
                Format = _ => TypeEditTextFormat.Multiline,
                Rows = _ => 6
            });

            general.Add(new ControlFormItemInputText
            {
                Name = _ => PollForm.Description,
                Label = _ => "kleenestar.poll:poll.description.label",
                Format = _ => TypeEditTextFormat.Wysiwyg,
                LinkLibraryUri = CoreHub.EditorLinkLibrary
            });

            var settings = new ControlFormItemGroupTabView("poll-tab-settings")
            {
                Title = _ => "kleenestar.poll:poll.tab.settings",
                Icon = _ => new IconGear()
            };

            settings.Add(new ControlFormItemGroupColumnHorizontal
            (
                new ControlFormItemInputDate
                {
                    Name = _ => PollForm.DeadlineDate,
                    Label = _ => "kleenestar.poll:poll.deadline.label",
                    Help = _ => "kleenestar.poll:poll.deadline.help"
                },
                new ControlFormItemInputText
                {
                    Name = _ => PollForm.DeadlineTime,
                    Label = _ => "kleenestar.poll:poll.deadlinetime.label",
                    Placeholder = _ => "23:59",
                    Pattern = _ => PollForm.TimePattern,
                    Icon = _ => new IconClock()
                }
            ));

            settings.Add(Switch(PollForm.AllowMaybe, "kleenestar.poll:poll.allowmaybe.label", "kleenestar.poll:poll.allowmaybe.help"));
            settings.Add(Switch(PollForm.MultipleChoice, "kleenestar.poll:poll.multiple.label", "kleenestar.poll:poll.multiple.help"));
            settings.Add(Switch(PollForm.Anonymous, "kleenestar.poll:poll.anonymous.label", "kleenestar.poll:poll.anonymous.help"));
            settings.Add(Switch(PollForm.HideResults, "kleenestar.poll:poll.hideresults.label", "kleenestar.poll:poll.hideresults.help"));

            var tabs = new ControlFormItemGroupTab();
            tabs.AddView(general, settings);

            return [tabs];
        }

        /// <summary>
        /// Builds a switch.
        /// </summary>
        /// <param name="name">The input name.</param>
        /// <param name="label">The internationalization key of the label.</param>
        /// <param name="help">The internationalization key of the help.</param>
        /// <returns>The switch.</returns>
        private static ControlFormItemInputCheck Switch(string name, string label, string help)
        {
            return new ControlFormItemInputCheck
            {
                Name = _ => name,
                Label = _ => label,
                Help = _ => help,
                Layout = _ => TypeLayoutCheck.Switch
            };
        }
    }
}
