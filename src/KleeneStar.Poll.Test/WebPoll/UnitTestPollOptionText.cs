using KleeneStar.Model.Entities;
using KleeneStar.Poll.WebPoll;
using System.Globalization;

namespace KleeneStar.Poll.Test.WebPoll
{
    /// <summary>
    /// Tests the options of a poll as the dialogs write them: one option per line.
    /// </summary>
    public class UnitTestPollOptionText
    {
        private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");
        private static readonly CultureInfo American = CultureInfo.GetCultureInfo("en-US");
        private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

        /// <summary>
        /// The forms of a proposed date are read in the culture of the reader and as ISO.
        /// </summary>
        [Fact]
        public void ParsesTheFormsOfADate()
        {
            var errors = new List<string>();
            var options = PollOptionText.Parse(string.Join("\n",
                "25.11.2026 14:00 - 15:30",
                "26.11.2026 9:30-10:15 | Raum 4.12",
                "27.11.2026 14:00",
                "2026-11-28",
                "30.11.2026 - 02.12.2026",
                "03.12.2026 22:00 - 04.12.2026 02:00",
                "",
                "25.11.2026 14:00 - 15:30"), true, TimeZoneInfo.Utc, German, Hour, errors);

            Assert.Empty(errors);
            Assert.Equal(6, options.Count);

            Assert.Equal(new DateTime(2026, 11, 25, 14, 0, 0, DateTimeKind.Utc), options[0].Start);
            Assert.Equal(new DateTime(2026, 11, 25, 15, 30, 0, DateTimeKind.Utc), options[0].End);
            Assert.False(options[0].AllDay);

            Assert.Equal(new DateTime(2026, 11, 26, 9, 30, 0, DateTimeKind.Utc), options[1].Start);
            Assert.Equal("Raum 4.12", options[1].Text);

            Assert.Equal(options[2].Start + Hour, options[2].End);

            Assert.True(options[3].AllDay);
            Assert.Equal(new DateTime(2026, 11, 29, 0, 0, 0, DateTimeKind.Utc), options[3].End);

            Assert.True(options[4].AllDay);
            Assert.Equal(new DateTime(2026, 12, 3, 0, 0, 0, DateTimeKind.Utc), options[4].End);

            Assert.Equal(new DateTime(2026, 12, 4, 2, 0, 0, DateTimeKind.Utc), options[5].End);
        }

        /// <summary>
        /// The times are wall-clock times of the reader's zone and stored in UTC.
        /// </summary>
        [Fact]
        public void ConvertsTheZone()
        {
            var berlin = PollTime.Zone("Europe/Berlin");
            var options = PollOptionText.Parse("01.07.2026 10:00 - 11:00", true, berlin, German, Hour, null);

            Assert.Equal(new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc), Assert.Single(options).Start);
        }

        /// <summary>
        /// A line that is no date is reported with its number; an end before the start is none.
        /// </summary>
        [Fact]
        public void ReportsWhatIsNoDate()
        {
            var errors = new List<string>();
            var options = PollOptionText.Parse("tomorrow\n25.11.2026 15:00 - 14:00\n25.11.2026 25:00\n25.11.2026", true, TimeZoneInfo.Utc, German, Hour, errors);

            Assert.Single(options);
            Assert.Equal(["1: tomorrow", "2: 25.11.2026 15:00 - 14:00", "3: 25.11.2026 25:00"], errors);
        }

        /// <summary>
        /// The choices of a decision are their lines, without duplicates and blanks.
        /// </summary>
        [Fact]
        public void ParsesChoices()
        {
            var options = PollOptionText.Parse(" Red \n\nGreen\nred\nBlue - dark", false, TimeZoneInfo.Utc, German, Hour, null);

            Assert.Equal(["Red", "Green", "Blue - dark"], options.Select(x => x.Text));
            Assert.All(options, x => Assert.Null(x.Start));
        }

        /// <summary>
        /// What is written is read back as the same options, in either culture.
        /// </summary>
        [Theory]
        [InlineData("de-DE")]
        [InlineData("en-US")]
        public void WriteRoundTrips(string name)
        {
            var culture = CultureInfo.GetCultureInfo(name);
            var zone = PollTime.Zone("Europe/Berlin");
            var source = PollOptionText.Parse(string.Join("\n",
                "2026-11-25 14:00 - 15:30",
                "2026-11-26 09:30 | Room",
                "2026-11-28",
                "2026-11-30 - 2026-12-02",
                "2026-12-03 22:00 - 2026-12-04 02:00"), true, zone, culture, Hour, null);

            var written = PollOptionText.Write(source, zone, culture);
            var read = PollOptionText.Parse(written, true, zone, culture, Hour, null);

            Assert.Equal(source.Count, read.Count);

            for (var i = 0; i < source.Count; i++)
            {
                Assert.True(PollOptionText.Same(source[i], read[i]), written);
            }
        }

        /// <summary>
        /// Options read from a dialog take the ids of their stored equals, so they keep their
        /// votes; a changed option is a new one.
        /// </summary>
        [Fact]
        public void MatchKeepsTheIdsOfUnchangedOptions()
        {
            var stored = PollOptionText.Parse("A\nB\nC", false, TimeZoneInfo.Utc, American, Hour, null);
            var read = PollOptionText.Parse("C\nA\nB2", false, TimeZoneInfo.Utc, American, Hour, null);

            var matched = PollOptionText.Match(read, stored);

            Assert.Equal(stored[2].Id, matched[0].Id);
            Assert.Equal(stored[0].Id, matched[1].Id);
            Assert.DoesNotContain(matched[2].Id, stored.Select(x => x.Id));
        }

        /// <summary>
        /// A proposed date is written in the zone and culture of the reader.
        /// </summary>
        [Fact]
        public void DescribesADate()
        {
            var option = new PollOption
            {
                Start = new DateTime(2026, 11, 25, 13, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2026, 11, 25, 14, 30, 0, DateTimeKind.Utc),
                Text = "Raum 4.12"
            };

            Assert.Equal("Mi, 25.11.2026 14:00 – 15:30 · Raum 4.12", PollTime.Describe(option, PollTime.Zone("Europe/Berlin"), German));
            Assert.Equal("Option", PollTime.Describe(new PollOption { Text = "Option" }, TimeZoneInfo.Utc, German));
        }
    }
}
