using KleeneStar.Core;
using KleeneStar.Poll.WebManager;
using KleeneStar.Poll.WebPoll;
using Microsoft.Extensions.Configuration;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Poll
{
    /// <summary>
    /// The service locator of the poll plugin, the counterpart of the core's
    /// <see cref="CoreHub"/>: the plugin's managers, resolved lazily from the component hub, and
    /// the plugin's settings.
    /// </summary>
    public static class PollHub
    {
        private static PollManager _pollManager;
        private static PollTypeManager _pollTypeManager;
        private static PollTypeManager _pollTypeManagerDetached;
        private static PollSettings _settings;

        /// <summary>
        /// Gets the component hub the plugin was started with; the core's when unset.
        /// </summary>
        public static IComponentHub ComponentHub { get; internal set; }

        /// <summary>
        /// Gets or sets the configuration section of the plugin.
        /// </summary>
        public static IConfiguration Configuration
        {
            get;
            internal set
            {
                field = value;
                _settings = null;
            }
        }

        /// <summary>
        /// Gets the settings of the plugin.
        /// </summary>
        public static PollSettings Settings => _settings ??= PollSettings.From(Configuration);

        /// <summary>
        /// Gets the manager of the settings, options and votes of polls.
        /// </summary>
        public static IPollManager PollManager => _pollManager ??= (ComponentHub ?? CoreHub.ComponentHub)?.GetComponentManager<PollManager>();

        /// <summary>
        /// Gets the catalog of poll types; outside a host one knowing the plugin's own types.
        /// </summary>
        public static IPollTypeManager PollTypeManager => _pollTypeManager
            ?? (_pollTypeManager = (ComponentHub ?? CoreHub.ComponentHub)?.GetComponentManager<PollTypeManager>())
            ?? (_pollTypeManagerDetached ??= WebManager.PollTypeManager.CreateDetached());
    }
}
