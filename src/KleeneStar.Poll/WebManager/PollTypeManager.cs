using KleeneStar.Poll.WebPoll;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin;

namespace KleeneStar.Poll.WebManager
{
    /// <summary>
    /// The catalog of poll types.
    /// </summary>
    public interface IPollTypeManager : IComponentManager
    {
        /// <summary>
        /// Gets the poll types, by their order.
        /// </summary>
        IEnumerable<IPollType> Types { get; }

        /// <summary>
        /// Returns the type of a key.
        /// </summary>
        /// <param name="key">The key, compared without case.</param>
        /// <returns>The type, or <see langword="null"/> for an unknown key.</returns>
        IPollType GetType(string key);

        /// <summary>
        /// Returns the type of a key, or the decision for an unknown one, so a poll whose type
        /// went away with its plugin still opens.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The type.</returns>
        IPollType Resolve(string key);

        /// <summary>
        /// Registers a type that is no class of a plugin (a test stand-in).
        /// </summary>
        /// <param name="type">The type.</param>
        void Register(IPollType type);
    }

    /// <summary>
    /// The poll types of the installation, collected from the plugins by WebExpress's
    /// <see cref="PluginTypeCatalog{TItem}"/>: every <b>public sealed</b> <see cref="IPollType"/>
    /// with a public parameterless constructor of any plugin.
    /// </summary>
    public sealed class PollTypeManager : IPollTypeManager
    {
        private readonly PluginTypeCatalog<IPollType> _catalog = new(x => Normalize(x.Key));

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub, or <see langword="null"/> without a host.</param>
        /// <param name="httpServerContext">The HTTP server context, or <see langword="null"/>.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private PollTypeManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            if (componentHub?.PluginManager is not null)
            {
                _catalog.Attach(componentHub, httpServerContext);
            }
            else
            {
                // without a plugin manager (a test, a call before the host is up) the manager
                // knows its own assembly's types
                _catalog.Collect(typeof(PollTypeManager).Assembly);
            }
        }

        /// <summary>
        /// Creates a manager outside a host.
        /// </summary>
        /// <returns>A manager knowing the plugin's own types.</returns>
        internal static PollTypeManager CreateDetached()
        {
            return new PollTypeManager(null, null);
        }

        /// <inheritdoc/>
        public IEnumerable<IPollType> Types => [.. _catalog.Items
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)];

        /// <inheritdoc/>
        public IPollType GetType(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? null : _catalog.Find(Normalize(key));
        }

        /// <inheritdoc/>
        public IPollType Resolve(string key)
        {
            return GetType(key) ?? GetType(DecisionPollType.TypeKey) ?? new DecisionPollType();
        }

        /// <inheritdoc/>
        public void Register(IPollType type)
        {
            ArgumentNullException.ThrowIfNull(type);

            _catalog.Register(type);
        }

        /// <summary>
        /// Normalizes a key: trimmed and lower case.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The normalized key.</returns>
        public static string Normalize(string key)
        {
            return key?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        /// <summary>
        /// Releases the catalog's subscription to the plugin manager.
        /// </summary>
        public void Dispose()
        {
            _catalog.Dispose();
        }
    }
}
