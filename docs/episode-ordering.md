# Episode Ordering (TVDB Overlay)

## Summary and constraints

- Sonarr issue #255 requested selectable episode ordering beyond aired order.
- Follow-up discussion in #7732 expands this to production/alternate style ordering.
- Skyhook remains source-of-truth for baseline series + episode payloads; this implementation does **not** require Skyhook changes.
- Alternate ordering data is fetched directly from TVDB and overlaid onto Skyhook episodes by TVDB episode ID.

## Chosen architecture

1. Skyhook fetch continues unchanged and returns aired ordering episodes.
2. During refresh, a single choke point (`RefreshSeriesService`) calls `EpisodeOrderingService` before episodes are persisted.
3. `EpisodeOrderingService` fetches optional TVDB ordering mappings (DVD/Alternate) and overlays season/episode values by `TvdbId`.
4. Persisted episode numbers then naturally flow into import/matching/renaming/search behavior, minimizing invasive changes elsewhere.

## API / UI / DB changes

- **DB**: add `Series.EpisodeOrdering` (`int`, default `Aired`) migration.
- **Core model**: add `EpisodeOrderingType` enum (`Aired`, `Absolute`, `Dvd`, `Alternate`) and `Series.EpisodeOrdering`.
- **API**:
  - `SeriesResource` includes `episodeOrdering`.
  - `SeriesEditorResource` supports bulk edit of `episodeOrdering`.
- **UI**:
  - single series edit now includes Episode Ordering select.
  - multi-series edit includes Episode Ordering with “No Change”.

## TVDB fetch + cache

- Added `TvdbEpisodeOrderProxy` to request TVDB order endpoints for DVD / Alternate ordering.
- Added in-memory cache per series + ordering with a 6 hour TTL to avoid excess TVDB calls.

## Fallback behavior

- If selected order is `Aired`, no overlay is applied.
- `Absolute` keeps existing behavior (episode refresh uses current data; anime absolute mapping path unchanged).
- If TVDB order lookup fails, returns empty, or has no usable mappings:
  - Sonarr falls back to aired numbering,
  - warning is logged with series + ordering context.

## Authentication (required for DVD / Alternate)

TheTVDB v4 API requires a bearer token; unauthenticated calls fail. The proxy now logs in via
`/v4/login` using values from `config.xml`:

```xml
<TvdbApiKey>your-tvdb-v4-api-key</TvdbApiKey>
<TvdbSubscriberPin>your-subscriber-pin</TvdbSubscriberPin> <!-- only if your key requires a pin -->
```

Tokens are cached for 7 days. Without a key, DVD/Alternate ordering logs a warning and falls
back to aired numbering.

## Scene numbering interplay (the American Dad fix)

When a series' ordering is pinned to anything other than `Aired`, XEM/scene numbering is disabled
for that series so the user-selected numbering is authoritative end to end (Sonarr/Sonarr#2086):

- `XemService` clears stored scene numbers on refresh and when the ordering changes, and stops
  applying XEM mappings while pinned; switching back to `Aired` reapplies them.
- `ParsingService` maps releases with their literal numbering (no scene-to-TVDB season shifts).
- `ReleaseSearchService` searches using the series' own season/episode numbers; scene mappings
  are still used as alternative title sources.

## Refresh trigger and episode identity

- Changing `episodeOrdering` via the API/UI now queues a `RefreshSeriesCommand` automatically so
  the renumbering applies immediately.
- `RefreshEpisodeService` matches existing episodes by TVDB episode ID first (falling back to
  season/episode) so renumbering does not orphan episode files, history, or monitoring state.
