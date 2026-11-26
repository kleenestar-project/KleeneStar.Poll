using WebExpress.WebCore.WebIcon;

namespace KleeneStar.Poll.WebPoll
{
    /// <summary>
    /// A type of poll: what its options are and how a voter answers them. The plugin's catalog
    /// (<see cref="WebManager.PollTypeManager"/>) collects every <b>public sealed</b> class
    /// implementing the contract with a public parameterless constructor, from any plugin; the
    /// plugin ships the scheduling poll (<see cref="SchedulePollType"/>) and the decision
    /// (<see cref="DecisionPollType"/>). There is no enum of poll types: the persisted
    /// <c>Poll.Type</c> is the <see cref="Key"/>.
    /// </summary>
    /// <remarks>
    /// The two traits describe every poll the plugin can run:
    /// <list type="bullet">
    ///   <item><see cref="Dated"/>: the options are proposed dates (start, end or whole days)
    ///   rather than texts.</item>
    ///   <item><see cref="Graded"/>: the voter answers every option with yes, no or - where the
    ///   poll allows it - if need be; otherwise the voter chooses one option or, where the poll
    ///   allows it, several.</item>
    /// </list>
    /// </remarks>
    public interface IPollType
    {
        /// <summary>
        /// Gets the persisted key of the type, lower case (<c>schedule</c>, <c>decision</c>).
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Gets the internationalization key of the display name.
        /// </summary>
        string Label { get; }

        /// <summary>
        /// Gets the internationalization key of a sentence describing the type.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the icon of the type.
        /// </summary>
        IIcon Icon { get; }

        /// <summary>
        /// Gets the display order among the types offered.
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Gets whether the options are proposed dates.
        /// </summary>
        bool Dated { get; }

        /// <summary>
        /// Gets whether the voter answers every option (yes, if need be, no) instead of choosing.
        /// </summary>
        bool Graded { get; }
    }
}
