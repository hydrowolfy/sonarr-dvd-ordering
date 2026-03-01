using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace NzbDrone.Core.Tv
{
    public interface IEpisodeOrderingService
    {
        List<Episode> ApplyEpisodeOrdering(Series series, List<Episode> episodes);
    }

    public interface ITvdbEpisodeOrderProxy
    {
        List<TvdbEpisodeOrderMapping> GetEpisodeOrderMappings(int tvdbSeriesId, EpisodeOrderingType orderingType);
    }

    public class TvdbEpisodeOrderMapping
    {
        public int TvdbEpisodeId { get; set; }
        public int SeasonNumber { get; set; }
        public int EpisodeNumber { get; set; }
        public int? AbsoluteEpisodeNumber { get; set; }
    }

    public class EpisodeOrderingService : IEpisodeOrderingService
    {
        private readonly ITvdbEpisodeOrderProxy _tvdbEpisodeOrderProxy;
        private readonly Logger _logger;

        public EpisodeOrderingService(ITvdbEpisodeOrderProxy tvdbEpisodeOrderProxy, Logger logger)
        {
            _tvdbEpisodeOrderProxy = tvdbEpisodeOrderProxy;
            _logger = logger;
        }

        public List<Episode> ApplyEpisodeOrdering(Series series, List<Episode> episodes)
        {
            if (series.EpisodeOrdering == EpisodeOrderingType.Aired)
            {
                return episodes;
            }

            if (series.EpisodeOrdering == EpisodeOrderingType.Absolute)
            {
                return episodes;
            }

            List<TvdbEpisodeOrderMapping> mappings;

            try
            {
                mappings = _tvdbEpisodeOrderProxy.GetEpisodeOrderMappings(series.TvdbId, series.EpisodeOrdering);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Unable to retrieve {0} ordering for '{1}' ({2}), falling back to aired ordering", series.EpisodeOrdering, series.Title, series.TvdbId);
                return episodes;
            }

            if (mappings.Count == 0)
            {
                _logger.Warn("No {0} ordering data available for '{1}' ({2}), falling back to aired ordering", series.EpisodeOrdering, series.Title, series.TvdbId);
                return episodes;
            }

            var byTvdbId = mappings.GroupBy(m => m.TvdbEpisodeId).ToDictionary(g => g.Key, g => g.First());

            foreach (var episode in episodes)
            {
                if (!byTvdbId.TryGetValue(episode.TvdbId, out var mapping))
                {
                    continue;
                }

                episode.SeasonNumber = mapping.SeasonNumber;
                episode.EpisodeNumber = mapping.EpisodeNumber;

                if (mapping.AbsoluteEpisodeNumber.HasValue)
                {
                    episode.AbsoluteEpisodeNumber = mapping.AbsoluteEpisodeNumber;
                }
            }

            return episodes;
        }
    }
}
