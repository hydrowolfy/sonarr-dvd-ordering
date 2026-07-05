using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MetadataSource.Tvdb
{
    public class TvdbEpisodeOrderProxy : ITvdbEpisodeOrderProxy
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);
        private static readonly TimeSpan TokenTtl = TimeSpan.FromDays(7);

        private readonly IHttpClient _httpClient;
        private readonly IConfigFileProvider _configFileProvider;
        private readonly ICached<List<TvdbEpisodeOrderMapping>> _cache;
        private readonly ICached<string> _tokenCache;
        private readonly Logger _logger;

        public TvdbEpisodeOrderProxy(IHttpClient httpClient, IConfigFileProvider configFileProvider, ICacheManager cacheManager, Logger logger)
        {
            _httpClient = httpClient;
            _configFileProvider = configFileProvider;
            _logger = logger;
            _cache = cacheManager.GetCache<List<TvdbEpisodeOrderMapping>>(GetType(), "episode_order");
            _tokenCache = cacheManager.GetCache<string>(GetType(), "token");
        }

        public List<TvdbEpisodeOrderMapping> GetEpisodeOrderMappings(int tvdbSeriesId, EpisodeOrderingType orderingType)
        {
            var cacheKey = $"{tvdbSeriesId}:{orderingType}";

            return _cache.Get(cacheKey, () => FetchEpisodeMappings(tvdbSeriesId, orderingType), CacheTtl);
        }

        private string GetAuthToken()
        {
            var apiKey = _configFileProvider.TvdbApiKey;

            if (apiKey.IsNullOrWhiteSpace())
            {
                return null;
            }

            return _tokenCache.Get("token", () =>
            {
                var loginRequest = new HttpRequestBuilder("https://api4.thetvdb.com/v4/login")
                    .Post()
                    .Accept(HttpAccept.Json)
                    .Build();

                loginRequest.Headers.ContentType = "application/json";
                loginRequest.SetContent(new { apikey = apiKey, pin = _configFileProvider.TvdbSubscriberPin }.ToJson());
                loginRequest.SuppressHttpError = true;

                var loginResponse = _httpClient.Post<string>(loginRequest);

                if (loginResponse.HasHttpError || string.IsNullOrWhiteSpace(loginResponse.Resource))
                {
                    _logger.Warn("TVDB v4 login failed, check the TvdbApiKey and TvdbSubscriberPin values in config.xml");
                    return null;
                }

                var parsedLogin = JsonConvert.DeserializeObject<TvdbLoginResponse>(loginResponse.Resource);

                return parsedLogin?.Data?.Token;
            }, TokenTtl);
        }

        private List<TvdbEpisodeOrderMapping> FetchEpisodeMappings(int tvdbSeriesId, EpisodeOrderingType orderingType)
        {
            var orderType = orderingType switch
            {
                EpisodeOrderingType.Dvd => "dvd",
                EpisodeOrderingType.Alternate => "alternate",
                _ => "official"
            };

            var token = GetAuthToken();

            if (token == null)
            {
                _logger.Warn("A TheTVDB v4 API key is required for {0} episode ordering. Add <TvdbApiKey> (and <TvdbSubscriberPin> if applicable) to config.xml. Falling back to aired ordering for series {1}", orderingType, tvdbSeriesId);
                return [];
            }

            var request = new HttpRequestBuilder($"https://api4.thetvdb.com/v4/series/{tvdbSeriesId}/episodes/{orderType}")
                .SetHeader("Authorization", $"Bearer {token}")
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

        private class TvdbLoginResponse
        {
            [JsonProperty("data")]
            public TvdbLoginData Data { get; set; }
        }

        private class TvdbLoginData
        {
            [JsonProperty("token")]
            public string Token { get; set; }
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
