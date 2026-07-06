using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DataAugmentation.Scene;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.ParserTests.ParsingServiceTests
{
    [TestFixture]
    public class EpisodeOrderingFixture : TestBase<ParsingService>
    {
        private Series _series;
        private ParsedEpisodeInfo _parsedEpisodeInfo;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>.CreateNew()
                .With(s => s.Title = "American Dad!")
                .With(s => s.CleanTitle = "americandad")
                .With(s => s.UseSceneNumbering = true)
                .With(s => s.EpisodeOrdering = EpisodeOrderingType.Aired)
                .Build();

            _parsedEpisodeInfo = new ParsedEpisodeInfo
            {
                SeriesTitle = _series.Title,
                ReleaseTitle = "American.Dad.S13E17.720p.WEB-DL",
                SeasonNumber = 13,
                EpisodeNumbers = new[] { 17 },
                AbsoluteEpisodeNumbers = System.Array.Empty<int>(),
                Languages = new List<Language> { Language.English }
            };

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.FindByTitle(It.IsAny<string>()))
                  .Returns(_series);
        }

        private void GivenSceneMapping()
        {
            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.FindByTvdbId(_series.TvdbId))
                  .Returns(_series);

            Mocker.GetMock<ISceneMappingService>()
                  .Setup(s => s.FindSceneMapping(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                  .Returns(new SceneMapping
                  {
                      TvdbId = _series.TvdbId,
                      Title = "American Dad!",
                      SearchTerm = "American Dad!",
                      SeasonNumber = 14,
                      SceneSeasonNumber = 13,
                      Type = "XemService"
                  });
        }

        [Test]
        public void should_apply_scene_mapping_season_offset_when_ordering_is_aired()
        {
            GivenSceneMapping();

            var result = Subject.Map(_parsedEpisodeInfo, _series.TvdbId, _series.TvRageId, _series.ImdbId);

            result.MappedSeasonNumber.Should().Be(14);
        }

        [Test]
        public void should_not_apply_scene_mapping_season_offset_when_ordering_is_pinned()
        {
            GivenSceneMapping();

            _series.EpisodeOrdering = EpisodeOrderingType.Dvd;

            var result = Subject.Map(_parsedEpisodeInfo, _series.TvdbId, _series.TvRageId, _series.ImdbId);

            result.MappedSeasonNumber.Should().Be(13);

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisode(_series.Id, 13, 17), Times.Once());
        }

        [Test]
        public void should_not_use_scene_numbering_lookup_when_ordering_is_pinned()
        {
            _series.EpisodeOrdering = EpisodeOrderingType.Dvd;

            Subject.Map(_parsedEpisodeInfo, _series.TvdbId, _series.TvRageId, _series.ImdbId);

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisodesBySceneNumbering(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never());

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisode(_series.Id, 13, 17), Times.Once());
        }

        [Test]
        public void should_use_scene_numbering_lookup_when_ordering_is_aired()
        {
            Subject.Map(_parsedEpisodeInfo, _series.TvdbId, _series.TvRageId, _series.ImdbId);

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisodesBySceneNumbering(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Once());
        }
    }
}
