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
