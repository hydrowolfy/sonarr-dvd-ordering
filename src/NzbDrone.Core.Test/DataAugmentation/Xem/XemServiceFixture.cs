using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Cache;
using NzbDrone.Core.DataAugmentation.Xem;
using NzbDrone.Core.DataAugmentation.Xem.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.Test.DataAugmentation.Xem
{
    [TestFixture]
    public class XemServiceFixture : CoreTest<XemService>
    {
        private Series _series;
        private List<Episode> _episodes;

        [SetUp]
        public void Setup()
        {
            Mocker.SetConstant<ICacheManager>(Mocker.Resolve<CacheManager>());

            _series = Builder<Series>.CreateNew()
                .With(s => s.TvdbId = 73141)
                .With(s => s.UseSceneNumbering = true)
                .With(s => s.EpisodeOrdering = EpisodeOrderingType.Dvd)
                .Build();

            _episodes = Builder<Episode>.CreateListOfSize(2)
                .All()
                .With(e => e.SeriesId = _series.Id)
                .With(e => e.SceneSeasonNumber = 13)
                .With(e => e.SceneEpisodeNumber = 17)
                .With(e => e.SceneAbsoluteEpisodeNumber = 250)
                .Build()
                .ToList();

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodeBySeries(_series.Id))
                  .Returns(_episodes);

            Mocker.GetMock<IXemProxy>()
                  .Setup(s => s.GetXemSeriesIds())
                  .Returns(new List<int> { _series.TvdbId });
        }

        [Test]
        public void should_clear_scene_numbering_when_ordering_is_pinned_on_refresh()
        {
            Subject.Handle(new SeriesUpdatedEvent(_series));

            Mocker.GetMock<IXemProxy>()
                  .Verify(v => v.GetSceneTvdbMappings(It.IsAny<int>()), Times.Never());

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.UpdateEpisodes(It.Is<List<Episode>>(e =>
                      e.Count == 2 &&
                      e.All(x => x.SceneSeasonNumber == null && x.SceneEpisodeNumber == null && x.SceneAbsoluteEpisodeNumber == null))), Times.Once());

            Mocker.GetMock<ISeriesService>()
                  .Verify(v => v.UpdateSeries(It.Is<Series>(s => s.UseSceneNumbering == false), It.IsAny<bool>(), It.IsAny<bool>()), Times.Once());
        }

        [Test]
        public void should_clear_scene_numbering_when_ordering_changed_to_pinned()
        {
            var oldSeries = Builder<Series>.CreateNew()
                .With(s => s.TvdbId = _series.TvdbId)
                .With(s => s.EpisodeOrdering = EpisodeOrderingType.Aired)
                .Build();

            Subject.Handle(new SeriesEditedEvent(_series, oldSeries));

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.UpdateEpisodes(It.IsAny<List<Episode>>()), Times.Once());

            Mocker.GetMock<ISeriesService>()
                  .Verify(v => v.UpdateSeries(It.Is<Series>(s => s.UseSceneNumbering == false), It.IsAny<bool>(), It.IsAny<bool>()), Times.Once());
        }

        [Test]
        public void should_not_clear_scene_numbering_when_ordering_did_not_change()
        {
            var oldSeries = Builder<Series>.CreateNew()
                .With(s => s.TvdbId = _series.TvdbId)
                .With(s => s.EpisodeOrdering = EpisodeOrderingType.Dvd)
                .Build();

            Subject.Handle(new SeriesEditedEvent(_series, oldSeries));

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.UpdateEpisodes(It.IsAny<List<Episode>>()), Times.Never());
        }

        [Test]
        public void should_apply_mappings_when_ordering_is_aired_on_refresh()
        {
            _series.EpisodeOrdering = EpisodeOrderingType.Aired;

            Mocker.GetMock<IXemProxy>()
                  .Setup(s => s.GetSceneTvdbMappings(_series.TvdbId))
                  .Returns(new List<XemSceneTvdbMapping>());

            Subject.Handle(new SeriesUpdatedEvent(_series));

            Mocker.GetMock<IXemProxy>()
                  .Verify(v => v.GetSceneTvdbMappings(_series.TvdbId), Times.Once());
        }
    }
}
