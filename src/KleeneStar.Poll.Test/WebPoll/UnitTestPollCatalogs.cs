using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebWorkspaceTemplate;
using KleeneStar.Model.Entities;
using KleeneStar.Poll.WebManager;
using KleeneStar.Poll.WebPoll;
using System.Reflection;
using PollEntity = KleeneStar.Model.Entities.Poll;

namespace KleeneStar.Poll.Test.WebPoll
{
    /// <summary>
    /// Tests the items the plugin contributes to catalogs: they are collected only as
    /// <b>public sealed</b> classes with a public parameterless constructor.
    /// </summary>
    public class UnitTestPollCatalogs
    {
        /// <summary>
        /// Every catalog item of the plugin can be collected.
        /// </summary>
        [Theory]
        [InlineData(typeof(IPollType))]
        [InlineData(typeof(IObjectKind))]
        [InlineData(typeof(IWorkspaceTemplate))]
        public void ItemsAreCollectable(Type contract)
        {
            var items = typeof(PollHub).Assembly.GetTypes()
                .Where(x => contract.IsAssignableFrom(x) && x.IsClass && !x.IsAbstract)
                .ToList();

            Assert.NotEmpty(items);
            Assert.All(items, x =>
            {
                Assert.True(x.IsPublic && x.IsSealed, x.FullName);
                Assert.NotNull(x.GetConstructor(BindingFlags.Instance | BindingFlags.Public, Type.EmptyTypes));
            });
        }

        /// <summary>
        /// The catalog outside a host knows the scheduling poll and the decision, by their order,
        /// and resolves an unknown type as a decision.
        /// </summary>
        [Fact]
        public void DetachedCatalogKnowsTheShippedTypes()
        {
            var catalog = PollTypeManager.CreateDetached();

            Assert.Equal([SchedulePollType.TypeKey, DecisionPollType.TypeKey], catalog.Types.Select(x => x.Key));
            Assert.True(catalog.GetType(" Schedule ").Graded);
            Assert.Null(catalog.GetType("lottery"));
            Assert.Equal(DecisionPollType.TypeKey, catalog.Resolve("lottery").Key);
        }

        /// <summary>
        /// The poll kind sits between the calendar (3) and the issues (5), and its key is the
        /// model's.
        /// </summary>
        [Fact]
        public void KindSitsBelowTheCalendar()
        {
            var kind = new PollKind();

            Assert.Equal(ObjectKind.Poll, kind.Key);
            Assert.Equal(4, kind.Order);
            Assert.False(kind.ServiceLevels);
        }

        /// <summary>
        /// An answer counts only where the poll allows it.
        /// </summary>
        [Fact]
        public void AnswersAreAllowedByType()
        {
            var graded = new PollEntity { AllowMaybe = false };

            Assert.True(PollResult.Allowed(graded, new SchedulePollType(), PollAnswer.No));
            Assert.False(PollResult.Allowed(graded, new SchedulePollType(), PollAnswer.Maybe));
            Assert.True(PollResult.Allowed(new PollEntity { AllowMaybe = true }, new SchedulePollType(), PollAnswer.Maybe));
            Assert.False(PollResult.Allowed(new PollEntity { AllowMaybe = true }, new DecisionPollType(), PollAnswer.Maybe));
            Assert.False(PollResult.Allowed(new PollEntity(), new DecisionPollType(), PollAnswer.No));
        }

        /// <summary>
        /// The settings default where nothing is configured.
        /// </summary>
        [Fact]
        public void SettingsDefault()
        {
            var settings = PollSettings.From(null);

            Assert.Equal(5, settings.AutoCloseInterval);
            Assert.Equal(10, settings.RunningCount);
            Assert.Equal(60, settings.DefaultDuration);
            Assert.Null(settings.TimeZone);
        }
    }
}
