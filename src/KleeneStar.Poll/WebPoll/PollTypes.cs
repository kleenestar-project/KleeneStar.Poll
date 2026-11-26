using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Poll.WebPoll
{
    /// <summary>
    /// The scheduling poll (<i>Zeitabstimmung</i>): a few dates are proposed, every voter says
    /// for each whether it suits (yes, if need be, no), and the date most can make is fixed.
    /// </summary>
    public sealed class SchedulePollType : IPollType
    {
        /// <summary>
        /// The persisted key of the type.
        /// </summary>
        public const string TypeKey = "schedule";

        /// <inheritdoc/>
        public string Key => TypeKey;

        /// <inheritdoc/>
        public string Label => "kleenestar.poll:type.schedule.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.poll:type.schedule.description";

        /// <inheritdoc/>
        public IIcon Icon => new IconCalendarCheck();

        /// <inheritdoc/>
        public int Order => 1;

        /// <inheritdoc/>
        public bool Dated => true;

        /// <inheritdoc/>
        public bool Graded => true;
    }

    /// <summary>
    /// The decision (<i>Entscheidung</i>): a question with options, every voter chooses one -
    /// or, where the poll allows it, several - and the option most chose is taken.
    /// </summary>
    public sealed class DecisionPollType : IPollType
    {
        /// <summary>
        /// The persisted key of the type.
        /// </summary>
        public const string TypeKey = "decision";

        /// <inheritdoc/>
        public string Key => TypeKey;

        /// <inheritdoc/>
        public string Label => "kleenestar.poll:type.decision.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.poll:type.decision.description";

        /// <inheritdoc/>
        public IIcon Icon => new IconScaleBalanced();

        /// <inheritdoc/>
        public int Order => 2;

        /// <inheritdoc/>
        public bool Dated => false;

        /// <inheritdoc/>
        public bool Graded => false;
    }
}
