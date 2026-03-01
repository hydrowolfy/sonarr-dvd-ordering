using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Http;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MetadataSource.Tvdb
{
    public class TvdbEpisodeOrderProxy : ITvdbEpisodeOrderProxy
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);

        private readonly IHttpClient _httpClient;
        private readonly ICached<List<TvdbEpisodeOrderMapping>> _cache;
        private readonly Logger _logger;

        public TvdbEpisodeOrderProxy(IHttpClient httpClient, ICacheManager cacheManager, Logger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _cache = cacheManager.GetCache<List<TvdbEpisodeOrderMapping>>(GetType(), "episode_order");
        }

        public List<TvdbEpisodeOrderMapping> GetEpisodeOrderMappings(int tvdbSeriesId, EpisodeOrderingType orderingType)
        {
            var cacheKey = $"{tvdbSeriesId}:{orderingType}";

            return _cache.Get(cacheKey, () => FetchEpisodeMappings(tvdbSeriesId, orderingType), CacheTtl);
        }

        private List<TvdbEpisodeOrderMapping> FetchEpisodeMappings(int tvdbSeriesId, EpisodeOrderingType orderingType)
        {
            var orderType = orderingType switch
            {
                EpisodeOrderingType.Dvd => "dvd",
                EpisodeOrderingType.Alternate => "alternate",
                _ => "official"
            };

            var request = new HttpRequestBuilder($"https://api4.thetvdb.com/v4/series/{tvdbSeriesId}/episodes/{orderType}")
                .Build();

            request.SuppressHttpError = true;

            var response = _httpClient.Get<string>(request);

            if (response.HasHttpError || string.IsNullOrWhiteSpace(response.Resource))
            {
                _logger.Warn("TVDB episode ordering call failed for series {0} ({1})", tvdbSeriesId, orderingType);
                return [];
            }

            var parsed = JsonConvert.DeserializeObject<TvdbEpisodeOrderResponse>(response.Resource);

            if (parsed?.Data == null)
            {
                return [];
            }

            return parsed.Data.Where(d => d.Id > 0 && d.SeasonNumber >= 0 && d.Number > 0)
                .Select(d => new TvdbEpisodeOrderMapping
                {
                    TvdbEpisodeId = d.Id,
                    SeasonNumber = d.SeasonNumber,
                    EpisodeNumber = d.Number,
                    AbsoluteEpisodeNumber = d.AbsoluteNumber
                }).ToList();
        }

        private class TvdbEpisodeOrderResponse
        {
            [JsonProperty("data")]
            public List<TvdbEpisodeOrderEpisode> Data { get; set; }
        }

        private class TvdbEpisodeOrderEpisode
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("seasonNumber")]
            public int SeasonNumber { get; set; }

            [JsonProperty("number")]
            public int Number { get; set; }

            [JsonProperty("absoluteNumber")]
            public int? AbsoluteNumber { get; set; }
        }
    }
}
