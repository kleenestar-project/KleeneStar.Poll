using KleeneStar.Core;
using KleeneStar.Poll.WebPoll;
using System;
using WebExpress.WebCore;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin;

namespace KleeneStar.Poll
{
    /// <summary>
    /// Plugin entry point of the polls: the object kind <c>poll</c>, listed below the calendar in
    /// the sidebar of a workspace, with scheduling polls (which date suits everybody) and
    /// decisions (which option is taken), votes, results, deadlines and the closing of polls
    /// whose deadline ran out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A poll is an ordinary object of a class of the kind - its question, description,
    /// workflow, comments, attachments and history are the object's, and the core's detail
    /// experience shows them. What an object has no column for is kept in the model's poll
    /// tables (<c>Poll</c>, <c>PollOption</c>, <c>PollVote</c>), because the installation has one
    /// database and one migration history; without the plugin they are not read.
    /// </para>
    /// <para>
    /// The plugin's pages refuse a caller themselves (<see cref="PollAuthorization"/>), since the
    /// core's route authorization judges the core's pages only.
    /// </para>
    /// </remarks>
    [Name("kleenestar.poll:plugin.name")]
    [Description("kleenestar.poll:plugin.description")]
    [Icon("/assets/img/kleenestar.poll.svg")]
    [Application<KleeneStarApplication>()]
    [Dependency("webexpress.webapp")]
    [Dependency("kleenestar.core")]
    public sealed class KleeneStarPlugin : IPlugin
    {
        private readonly IHttpServerContext _httpServerContext;
        private readonly IPluginContext _pluginContext;
        private PollCloser _closer;

        /// <summary>
        /// Initializes a new instance of the class. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="httpServerContext">The server context, for the log.</param>
        /// <param name="pluginContext">The context of the plugin, for its settings.</param>
        /// <param name="componentHub">The component hub the plugin's managers are registered with.</param>
        public KleeneStarPlugin(IHttpServerContext httpServerContext, IPluginContext pluginContext, IComponentHub componentHub)
        {
            _httpServerContext = httpServerContext;
            _pluginContext = pluginContext;
            PollHub.ComponentHub = componentHub;
        }

        /// <summary>
        /// Connects the manager to the core's object and workflow events and starts closing the
        /// polls whose deadline ran out.
        /// </summary>
        public void Run()
        {
            try
            {
                PollHub.Configuration = _pluginContext?.Settings;

                var manager = PollHub.PollManager
                    ?? throw new InvalidOperationException("The poll manager is not registered.");

                manager.Connect();

                var interval = PollHub.Settings.AutoCloseInterval;

                if (interval > 0)
                {
                    _closer = new PollCloser(TimeSpan.FromMinutes(interval));
                }
            }
            catch (Exception ex)
            {
                _httpServerContext?.Log?.Exception(ex);
            }
        }

        /// <summary>
        /// Stops closing polls.
        /// </summary>
        public void Dispose()
        {
            _closer?.Dispose();
            _closer = null;
        }
    }
}
