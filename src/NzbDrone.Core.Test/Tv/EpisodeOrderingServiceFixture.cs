using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.Tv
{
    [TestFixture]
    public class EpisodeOrderingServiceFixture : CoreTest<EpisodeOrderingService>
    {
        [Test]
        public void should_overlay_dvd_ordering_when_mapping_exists()
        {
            var series = new Series
            {
                TvdbId = 123,
                Title = "DVD Fixture",
                EpisodeOrdering = EpisodeOrderingType.Dvd
            };

            var episodes = new List<Episode>
            {
                new Episode { TvdbId = 1001, SeasonNumber = 1, EpisodeNumber = 1, Title = "Pilot" },
                new Episode { TvdbId = 1002, SeasonNumber = 1, EpisodeNumber = 2, Title = "Second" }
            };

            Mocker.GetMock<ITvdbEpisodeOrderProxy>()
                  .Setup(v => v.GetEpisodeOrderMappings(series.TvdbId, EpisodeOrderingType.Dvd))
                  .Returns(new List<TvdbEpisodeOrderMapping>
                  {
                      new TvdbEpisodeOrderMapping { TvdbEpisodeId = 1001, SeasonNumber = 1, EpisodeNumber = 2 },
                      new TvdbEpisodeOrderMapping { TvdbEpisodeId = 1002, SeasonNumber = 1, EpisodeNumber = 1 }
                  });

            var result = Subject.ApplyEpisodeOrdering(series, episodes);

            result.Should().ContainSingle(e => e.TvdbId == 1001 && e.SeasonNumber == 1 && e.EpisodeNumber == 2);
            result.Should().ContainSingle(e => e.TvdbId == 1002 && e.SeasonNumber == 1 && e.EpisodeNumber == 1);
        }

        [Test]
        public void should_overlay_alternate_ordering_when_mapping_exists()
        {
            var series = new Series
            {
                TvdbId = 456,
                Title = "Alternate Fixture",
                EpisodeOrdering = EpisodeOrderingType.Alternate
            };

            var episodes = new List<Episode>
            {
                new Episode { TvdbId = 2001, SeasonNumber = 1, EpisodeNumber = 1, Title = "Part 1" },
                new Episode { TvdbId = 2002, SeasonNumber = 1, EpisodeNumber = 2, Title = "Part 2" }
            };

            Mocker.GetMock<ITvdbEpisodeOrderProxy>()
                  .Setup(v => v.GetEpisodeOrderMappings(series.TvdbId, EpisodeOrderingType.Alternate))
                  .Returns(new List<TvdbEpisodeOrderMapping>
                  {
                      new TvdbEpisodeOrderMapping { TvdbEpisodeId = 2001, SeasonNumber = 0, EpisodeNumber = 1 },
                      new TvdbEpisodeOrderMapping { TvdbEpisodeId = 2002, SeasonNumber = 1, EpisodeNumber = 1 }
                  });

            var result = Subject.ApplyEpisodeOrdering(series, episodes);

            result.Should().ContainSingle(e => e.TvdbId == 2001 && e.SeasonNumber == 0 && e.EpisodeNumber == 1);
            result.Should().ContainSingle(e => e.TvdbId == 2002 && e.SeasonNumber == 1 && e.EpisodeNumber == 1);
        }
    }
}
