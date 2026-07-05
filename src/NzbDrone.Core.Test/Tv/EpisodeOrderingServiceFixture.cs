using System;
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

        [TestCase(EpisodeOrderingType.Aired)]
        [TestCase(EpisodeOrderingType.Absolute)]
        public void should_not_fetch_mappings_when_ordering_does_not_use_overlay(EpisodeOrderingType orderingType)
        {
            var series = new Series
            {
                TvdbId = 123,
                Title = "Short Circuit Fixture",
                EpisodeOrdering = orderingType
            };

            var episodes = new List<Episode>
            {
                new Episode { TvdbId = 1001, SeasonNumber = 1, EpisodeNumber = 1 }
            };

            var result = Subject.ApplyEpisodeOrdering(series, episodes);

            result.Should().ContainSingle(e => e.TvdbId == 1001 && e.SeasonNumber == 1 && e.EpisodeNumber == 1);

            Mocker.GetMock<ITvdbEpisodeOrderProxy>()
                  .Verify(v => v.GetEpisodeOrderMappings(It.IsAny<int>(), It.IsAny<EpisodeOrderingType>()), Times.Never());
        }

        [Test]
        public void should_fall_back_to_aired_ordering_when_proxy_throws()
        {
            var series = new Series
            {
                TvdbId = 123,
                Title = "Throwing Fixture",
                EpisodeOrdering = EpisodeOrderingType.Dvd
            };

            var episodes = new List<Episode>
            {
                new Episode { TvdbId = 1001, SeasonNumber = 1, EpisodeNumber = 1 }
            };

            Mocker.GetMock<ITvdbEpisodeOrderProxy>()
                  .Setup(v => v.GetEpisodeOrderMappings(series.TvdbId, EpisodeOrderingType.Dvd))
                  .Throws(new Exception("boom"));

            var result = Subject.ApplyEpisodeOrdering(series, episodes);

            result.Should().ContainSingle(e => e.TvdbId == 1001 && e.SeasonNumber == 1 && e.EpisodeNumber == 1);
        }

        [Test]
        public void should_fall_back_to_aired_ordering_when_mappings_are_empty()
        {
            var series = new Series
            {
                TvdbId = 123,
                Title = "Empty Fixture",
                EpisodeOrdering = EpisodeOrderingType.Dvd
            };

            var episodes = new List<Episode>
            {
                new Episode { TvdbId = 1001, SeasonNumber = 1, EpisodeNumber = 1 }
            };

            Mocker.GetMock<ITvdbEpisodeOrderProxy>()
                  .Setup(v => v.GetEpisodeOrderMappings(series.TvdbId, EpisodeOrderingType.Dvd))
                  .Returns(new List<TvdbEpisodeOrderMapping>());

            var result = Subject.ApplyEpisodeOrdering(series, episodes);

            result.Should().ContainSingle(e => e.TvdbId == 1001 && e.SeasonNumber == 1 && e.EpisodeNumber == 1);
        }

        [Test]
        public void should_keep_aired_numbering_for_unmapped_episodes_when_no_collisions()
        {
            var series = new Series
            {
                TvdbId = 123,
                Title = "Partial Fixture",
                EpisodeOrdering = EpisodeOrderingType.Dvd
            };

            var episodes = new List<Episode>
            {
                new Episode { TvdbId = 1001, SeasonNumber = 1, EpisodeNumber = 1 },
                new Episode { TvdbId = 1002, SeasonNumber = 1, EpisodeNumber = 2 }
            };

            Mocker.GetMock<ITvdbEpisodeOrderProxy>()
                  .Setup(v => v.GetEpisodeOrderMappings(series.TvdbId, EpisodeOrderingType.Dvd))
                  .Returns(new List<TvdbEpisodeOrderMapping>
                  {
                      new TvdbEpisodeOrderMapping { TvdbEpisodeId = 1001, SeasonNumber = 2, EpisodeNumber = 1 }
                  });

            var result = Subject.ApplyEpisodeOrdering(series, episodes);

            result.Should().ContainSingle(e => e.TvdbId == 1001 && e.SeasonNumber == 2 && e.EpisodeNumber == 1);
            result.Should().ContainSingle(e => e.TvdbId == 1002 && e.SeasonNumber == 1 && e.EpisodeNumber == 2);
        }

        [Test]
        public void should_fall_back_to_aired_ordering_when_partial_mapping_causes_collisions()
        {
            var series = new Series
            {
                TvdbId = 123,
                Title = "Collision Fixture",
                EpisodeOrdering = EpisodeOrderingType.Dvd
            };

            var episodes = new List<Episode>
            {
                new Episode { TvdbId = 1001, SeasonNumber = 1, EpisodeNumber = 1 },
                new Episode { TvdbId = 1002, SeasonNumber = 1, EpisodeNumber = 2 }
            };

            // 1001 is remapped onto 1002's aired slot while 1002 has no mapping - a collision.
            Mocker.GetMock<ITvdbEpisodeOrderProxy>()
                  .Setup(v => v.GetEpisodeOrderMappings(series.TvdbId, EpisodeOrderingType.Dvd))
                  .Returns(new List<TvdbEpisodeOrderMapping>
                  {
                      new TvdbEpisodeOrderMapping { TvdbEpisodeId = 1001, SeasonNumber = 1, EpisodeNumber = 2 }
                  });

            var result = Subject.ApplyEpisodeOrdering(series, episodes);

            result.Should().ContainSingle(e => e.TvdbId == 1001 && e.SeasonNumber == 1 && e.EpisodeNumber == 1);
            result.Should().ContainSingle(e => e.TvdbId == 1002 && e.SeasonNumber == 1 && e.EpisodeNumber == 2);
        }
    }
}
