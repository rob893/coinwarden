# Technical Spec: Auction Data Sync Pipeline & APIs

**Status:** Draft
**Owner:** @rob893
**Scope:** Core data syncing jobs + time-series storage/retention/rollup + read APIs (incl. aggregates)
**Applies to:** Coinwarden API (rebuild of WoW Market Watcher on `starter-app-template`)

---

## 1. Overview

Coinwarden continuously ingests World of Warcraft auction-house (AH) data from the Blizzard APIs,
stores it as a price/supply time series, rolls it up into long-lived daily candlesticks, and exposes
read APIs for charts and market-intelligence features.

This spec covers **only** the foundational data pipeline:

1. **Syncing jobs** — pull commodity + per-realm auction data, discover and sync item metadata, and
   sync realm topology.
2. **Storage strategy** — how snapshots are stored, partitioned, retained, and aggregated under a
   constrained storage budget.
3. **Read APIs** — endpoints to query raw snapshots, aggregated candlesticks, items, and realms.

### 1.1 Goals

- Capture **hourly snapshots** of **all commodity items** (region-wide) and **all items on a
  configurable set of connected realms**.
- Preserve **item-variant granularity** (item bonuses / modifiers / pet attributes), not just base item
  IDs.
- Keep **full-resolution data for 30 days**, then retain **daily candlesticks indefinitely**.
- Operate within a small/cheap Azure Postgres footprint with a predictable storage growth story.
- Preserve the old app's **item-syncing** behavior (auction-driven item discovery → metadata backfill).

### 1.2 Non-goals (out of scope — future specs)

Watch lists, price alerts, authentication/authorization design, crafting profitability, gathering
analytics, flip finder, portfolio/inventory, market-intelligence narratives, notifications, and the UI
itself. This spec only references the **subscription hooks** that watch lists/alerts will later plug
into.

### 1.3 Confirmed product/architecture decisions

| Area | Decision |
|---|---|
| Region | **US only** to start (`dynamic-us`), region-aware design for later expansion |
| Commodities | **Region-wide, all items**, hourly |
| Per-realm scope | Configurable **allow-list** of connected realms **∪** watch-list/alert-driven realms+items |
| Granularity | **Preserve item bonuses/modifiers** — snapshots keyed by item *variant* |
| Storage engine | **Native Postgres declarative partitioning** + scheduled rollups (no TimescaleDB) |
| Storage tiers | **Hot Postgres** (allow-list + commodities) + **cold Azure Blob** archive (raw JSON landing zone + Parquet for *all* realms) |
| Raw retention | **30 days** of hourly snapshots (dropped by partition) |
| Aggregated retention | **Daily candlesticks kept indefinitely** |
| Background jobs | **Hangfire** (added to the template; per `vision.md`) |

---

## 2. Background & Constraints

### 2.1 Old app pain points (to fix)

- Stored **only commodities + items present in watch lists/alerts** — not all items.
- Retained only **~30 days** total (no long-term history); old rows deleted **row-by-row** (10k at a
  time) by `RemoveOldDataBackgroundJob`, which is slow and causes table bloat.
- **Discarded item-variant data** — `BlizzardAuctionItem` captured only `id` + `context`; `bonus_lists`
  and `modifiers` were dropped, so gear price history collapsed all variants into one series.
- Percentiles were computed by **materializing one array element per unit of quantity**
  (`Enumerable.Repeat(price, quantity)`), which is memory-heavy at scale.
- Used **Newtonsoft full-string deserialization** of very large auction payloads (commodities can be
  100 MB+), holding the entire response and object graph in memory.

### 2.2 Blizzard API shape (the constraint that drives storage)

- **Commodities** (`/data/wow/auctions/commodities`) — a **single region-wide dataset**. Items are
  stackable trade goods (herbs, ore, cloth, flasks, etc.) with **no bonuses/modifiers**. Uses
  `unit_price`. This is most of what gold-makers track and is **cheap to store in full**.
- **Per-realm auctions** (`/data/wow/connected-realm/{id}/auctions`) — **one dataset per connected
  realm** (~240 in US). Gear, BoEs, caged pets, recipes. Uses `bid`/`buyout`; items carry
  `bonus_lists[]`, `modifiers[]`, and pet attributes. This is the part that **explodes** storage,
  hence the allow-list.

### 2.3 Azure / Postgres constraints

- Target DB: **Azure Database for PostgreSQL Flexible Server v16** (from `starter-app-template`
  `CI/Azure/modules/postgres.bicep`), default **32 GB Burstable**.
- **TimescaleDB on Azure Flexible Server is the Apache-2 build only → native compression is
  unavailable** (the single biggest time-series storage win). Continuous aggregates may exist but are
  not relied upon.
- **Decision:** use **native Postgres declarative range partitioning** + Hangfire rollup/maintenance
  jobs. Zero extension/licensing risk, fully portable, and retention becomes an instant `DROP TABLE`
  of an old partition instead of mass `DELETE`.
- **Companion blob cold tier:** because in-Postgres compression is unavailable, an **Azure Blob
  Storage** account provides the cheap, compressed long tail — a raw-JSON landing zone for replay/DR
  plus a columnar **Parquet** archive that can capture *all* realms regardless of the Postgres hot
  allow-list (§5.6, §6.8). Postgres stays the hot/queryable tier; blob is the cheap archive. A
  self-managed VM with TSL TimescaleDB was considered and **rejected** (only ~50 GB available).

### 2.4 Item-variant granularity → cardinality impact

Preserving bonuses multiplies **per-realm gear** cardinality (a single item ID can appear as many
ilvl/affix variants). Commodities are unaffected (no bonuses). Storage projections (§5.5) apply a
gear-variant multiplier. The Postgres **hot-tier allow-list** therefore stays modest, while the **blob
Parquet archive (§5.6) captures all realms cheaply** regardless; size Postgres storage when the hot
allow-list grows.

---

## 3. Blizzard API Integration

A modernized `IBlizzardService` (port + clean up the old one) encapsulates all Blizzard calls.

### 3.1 Auth (OAuth 2.0 client credentials)

- `POST https://oauth.battle.net/token` (or `https://us.battle.net/oauth/token`) with HTTP Basic auth
  (`clientId:clientSecret`) and body `grant_type=client_credentials`.
- Response: `access_token`, `expires_in` (~24h). **Cache** the token in `IMemoryCache` until
  `expires_in - 60s`; refresh on demand and on a `401` (single retry), as the old service did.
- Credentials come from config/Key Vault (§8). Never logged.

### 3.2 Endpoints used

| Purpose | Method + Path | Namespace |
|---|---|---|
| Token | `POST oauth/token` | — |
| Connected realm index | `GET /data/wow/connected-realm/index` | `dynamic-us` |
| Connected realm detail | `GET /data/wow/connected-realm/{id}` | `dynamic-us` |
| **Per-realm auctions** | `GET /data/wow/connected-realm/{id}/auctions` | `dynamic-us` |
| **Commodities** | `GET /data/wow/auctions/commodities` | `dynamic-us` |
| Item detail | `GET /data/wow/item/{itemId}` | `static-us` |
| Item search (batch) | `GET /data/wow/search/item?id=A||B||C` (≤100 ids) | `static-us` |
| WoW token price | `GET /data/wow/token/index` | `dynamic-us` |

Base host: `https://us.api.blizzard.com`. Locale `en_US`.

### 3.3 Conditional GET (key optimization)

Both auction endpoints return a `Last-Modified` header and honor **`If-Modified-Since`**. Blizzard
refreshes AH data roughly **once per hour**, staggered per realm. The pull jobs **store the last
`Last-Modified` per source** (commodities + each connected realm) and send `If-Modified-Since`; a
**`304 Not Modified`** means *skip* — no parse, no insert. This avoids duplicate hourly rows and saves
significant CPU/IO.

### 3.4 Streaming deserialization

Auction payloads are large (commodities can exceed 100 MB). Use **`System.Text.Json` streaming**
(`Utf8JsonReader` over the `HttpResponseMessage` content stream) to iterate auctions without
materializing the whole document/object graph. This replaces the old full-string Newtonsoft approach.

### 3.5 Rate limits & error handling

- Blizzard limits: **~36,000 requests/hour** and **~100 requests/second** per client. Our hourly
  budget (1 commodities + N realms + occasional item batches) is far under this.
- Per-realm failures are retried with bounded attempts and re-queued (port the old queue + max-attempts
  loop), so one bad realm doesn't fail the whole run.
- All calls accept and propagate `CancellationToken`.

### 3.6 Captured auction fields (variant-defining)

`BlizzardAuctionItem` must be extended to capture everything that distinguishes a variant:

```jsonc
// per-realm auction entry
{
  "id": 123456789,
  "item": {
    "id": 168185,
    "context": 13,
    "bonus_lists": [4780, 6496, 1472],
    "modifiers": [{ "type": 9, "value": 132 }],
    "pet_breed_id": null, "pet_level": null,
    "pet_quality_id": null, "pet_species_id": null
  },
  "bid": 900000, "buyout": 1000000, "quantity": 1, "time_left": "VERY_LONG"
}
// commodity entry (no variant fields)
{ "id": 555, "item": { "id": 168327 }, "unit_price": 6000, "quantity": 200, "time_left": "SHORT" }
```

> Exact field presence (esp. pet fields, `modifiers` shape) **must be validated against a live API
> sample** during implementation; the variant signature (§4.3) is built only from the fields actually
> present.

---

## 4. Data Model

Two kinds of tables:

- **Dimension tables** (normal EF-managed): `Item`, `ItemVariant`, `ConnectedRealm`, `Realm`.
- **Fact tables** (native-partitioned, bulk-written): `AuctionSnapshot` (raw hourly),
  `AuctionDailyCandle` (rollup).

Prices are **copper** stored as `bigint` (a few million gold ≈ tens of billions of copper > `int`).

### 4.1 `Item` (metadata dimension)

Ported/trimmed from the old `WoWItem`. EF entity; PK = Blizzard item id (`int`).

| Column | Type | Notes |
|---|---|---|
| `Id` | int (PK) | Blizzard item id |
| `Name` | varchar(255) | |
| `Quality` | varchar(50) | common/rare/epic/… |
| `ItemClass` | varchar(50) | used by always-process filters |
| `ItemSubclass` | varchar(50) | |
| `InventoryType` | varchar(50) | |
| `Level`, `RequiredLevel` | int | base item level |
| `IsEquippable`, `IsStackable` | bool | |
| `IsCommodity` | bool | true if seen in commodities feed |
| `SellPrice`, `PurchasePrice`, `PurchaseQuantity`, `MaxCount` | bigint/int | vendor data |
| `MetadataSyncedAt` | timestamptz (nullable) | null ⇒ discovered but not yet backfilled |

### 4.2 `ItemVariant` (variant dimension)

Normalizes heavy bonus/modifier arrays **out of** the fact rows. One row per distinct
`(ItemId, VariantHash)`.

| Column | Type | Notes |
|---|---|---|
| `Id` | bigint (PK, identity) | surrogate, referenced by snapshots optionally (see §4.4) |
| `ItemId` | int (FK → Item) | |
| `VariantHash` | bytea(8) / bigint | deterministic hash of the canonical signature |
| `Context` | int (nullable) | |
| `BonusLists` | int[] | Postgres array |
| `Modifiers` | jsonb | array of `{type,value}` |
| `PetBreedId`/`PetLevel`/`PetQualityId`/`PetSpeciesId` | int (nullable) | caged pets |
| `IsBaseVariant` | bool | true when no bonuses/modifiers/pet (e.g., all commodities) |
| `FirstSeenAt` | timestamptz | |

Unique index `(ItemId, VariantHash)`. For commodities, every auction maps to the item's single
**base variant** (`VariantHash` = canonical empty signature).

### 4.3 Variant signature & hash (deterministic)

The signature must be **stable and order-independent** so the same physical item always hashes the same:

1. Sort `bonus_lists` ascending → `b1,b2,…`.
2. Sort `modifiers` by `type` → `t1:v1;t2:v2;…`.
3. Append pet fields if present → `pet=species:breed:quality:level`.
4. Compose `"<itemId>|ctx=<context>|bl=<…>|mod=<…>|pet=<…>"`.
5. `VariantHash = xxHash64(signature)` (8 bytes; collision-resistant enough at our cardinality and
   small to carry on fact rows). Store the full components on `ItemVariant` for reconstruction.

Base variant (no bonuses/modifiers/pet) hashes a fixed canonical empty string so all commodities and
plain items share their item's base variant.

### 4.4 `AuctionSnapshot` (raw hourly fact — partitioned)

One row per `(item variant, realm-or-region, hour)`. Carries the **`VariantHash` denormalized** so the
hot bulk-insert path does **not** require a synchronous FK lookup; `ItemVariant` is upserted separately
(§6.3). `ConnectedRealmId = 0` is the **region-commodity sentinel**.

| Column | Type | Notes |
|---|---|---|
| `SnapshotHour` | timestamptz | truncated to the hour (UTC); **partition key** |
| `ItemId` | int | |
| `VariantHash` | bytea(8) | base-variant value for commodities |
| `ConnectedRealmId` | int | `0` = region commodities |
| `Quantity` | bigint | total units available |
| `NumAuctions` | int | listing count |
| `MinUnitPrice` | bigint | cheapest unit price (copper) |
| `MaxUnitPrice` | bigint | |
| `AvgUnitPrice` | bigint | quantity-weighted mean |
| `MarketPrice` | bigint | robust value = qty-weighted mean of cheapest 15% of supply |
| `P25`,`P50`,`P75`,`P95` | bigint | quantity-weighted percentiles |

**Composite PK** `(SnapshotHour, ConnectedRealmId, ItemId, VariantHash)` — includes the partition key
(Postgres requirement) and gives natural **idempotency** (re-running an hour upserts the same key).

**Unit price** = commodities → `unit_price`; per-realm → `buyout ?? bid` (gear stacks are qty 1; when
`quantity > 1`, divide to per-unit). Quantity-weighted percentiles are computed **without** expanding
arrays: sort listings by unit price, accumulate quantity, and read the price where the cumulative
fraction crosses each target (fixes the old `Enumerable.Repeat` memory blow-up).

### 4.5 `AuctionDailyCandle` (rollup fact — partitioned, indefinite)

One row per `(item variant, realm-or-region, day)`, built from that day's up-to-24 snapshots.

| Column | Type | Notes |
|---|---|---|
| `CandleDate` | date | UTC day; **partition key** |
| `ItemId` | int | |
| `VariantHash` | bytea(8) | |
| `ConnectedRealmId` | int | `0` = region commodities |
| `Open`,`High`,`Low`,`Close` | bigint | OHLC of `MarketPrice` across the day's snapshots |
| `AvgMarketPrice` | bigint | mean of snapshot `MarketPrice` |
| `MinUnitPrice`,`MaxUnitPrice` | bigint | absolute extremes observed during the day |
| `AvgP50`,`AvgQuantity` | bigint | mean of hourly median / quantity |
| `MinQuantity`,`MaxQuantity`,`EndQuantity` | bigint | supply envelope + last reading |
| `SnapshotCount` | smallint | data completeness (≤24) |

**Composite PK** `(CandleDate, ConnectedRealmId, ItemId, VariantHash)`.

### 4.6 `ConnectedRealm` / `Realm` (topology dimensions)

Ported from old app. `ConnectedRealm { Id, Population, Region }`, `Realm { Id, Name, Slug, Category,
Timezone, ConnectedRealmId }`. `Region` added for future multi-region.

### 4.7 Realm-sync subscription (selection config)

What per-realm data to pull is computed from an `IRealmSyncSubscriptionProvider` abstraction returning
target `(connectedRealmId, itemSelector)` pairs. Two providers:

- **`ConfigAllowListSubscriptionProvider`** (implemented here) — reads the allow-list of connected
  realm ids + always-process item-class/subclass filters from settings (§8). Allow-listed realms
  capture **all** items (subject to optional item-class filter).
- **`WatchListAlertSubscriptionProvider`** (interface only here; implemented by the future watch-list/
  alert features) — yields realm+item pairs referenced by user watch lists/alerts, preserving the old
  dynamic behavior.

The pull job unions all providers' outputs. With no watch-list provider registered, behavior = "all
commodities + allow-listed realms."

---

## 5. Storage Tiers, Partitioning, Retention & Rollup

### 5.1 Native declarative partitioning

- `auction_snapshots` — `PARTITION BY RANGE (snapshot_hour)`, **one partition per UTC day**
  (≤ ~33 live: 30 days + a small buffer). Daily granularity gives precise retention and small drop
  units.
- `auction_daily_candles` — `PARTITION BY RANGE (candle_date)`, **one partition per month** (12/yr,
  retained indefinitely).

EF Core cannot model native partitions, so the **parent partitioned tables and their indexes are
created via raw SQL** in a migration (`migrationBuilder.Sql(...)`), and **child partitions are managed
at runtime** by `PartitionMaintenanceJob` (§6.6). Example DDL:

```sql
CREATE TABLE auction_snapshots (
  snapshot_hour      timestamptz NOT NULL,
  connected_realm_id int         NOT NULL,
  item_id            int         NOT NULL,
  variant_hash       bytea       NOT NULL,
  quantity           bigint      NOT NULL,
  num_auctions       int         NOT NULL,
  min_unit_price     bigint      NOT NULL,
  max_unit_price     bigint      NOT NULL,
  avg_unit_price     bigint      NOT NULL,
  market_price       bigint      NOT NULL,
  p25 bigint NOT NULL, p50 bigint NOT NULL, p75 bigint NOT NULL, p95 bigint NOT NULL,
  PRIMARY KEY (snapshot_hour, connected_realm_id, item_id, variant_hash)
) PARTITION BY RANGE (snapshot_hour);

-- daily child, pre-created by the maintenance job
CREATE TABLE auction_snapshots_2026_06_23
  PARTITION OF auction_snapshots
  FOR VALUES FROM ('2026-06-23') TO ('2026-06-24');
```

### 5.2 Retention (raw)

`PartitionMaintenanceJob` drops `auction_snapshots` partitions whose range is **older than 30 days**:
`DROP TABLE auction_snapshots_<date>` — **instant, no bloat, no vacuum churn**. Replaces the old
row-by-row delete job entirely.

### 5.3 Rollup (raw → daily candles)

`RollupDailyCandlesJob` runs daily (after the UTC day closes, before purge) and, for the **previous
day**, aggregates all `auction_snapshots` into `auction_daily_candles` (OHLC of `MarketPrice` + supply
envelope + completeness). Idempotent via `INSERT … ON CONFLICT (…) DO UPDATE`. A single set-based
`INSERT … SELECT … GROUP BY item_id, variant_hash, connected_realm_id` does the work in the database
(no row shipping to the app). Open/Close use `first_value`/`last_value` window functions ordered by
`snapshot_hour`.

### 5.4 Indexing

- Fact tables: the composite PK already indexes `(time, realm, item, variant)`. Add a **local**
  secondary index `(item_id, connected_realm_id, snapshot_hour)` (and the candle equivalent) for the
  primary read path "one item on one realm over a time range." Keep indexes minimal — each one slows the
  bulk-insert/merge.
- Dimensions: `Item(Name)` (search), `Item(ItemClass, ItemSubclass)` (always-process filter),
  `ItemVariant(ItemId, VariantHash)` unique.

### 5.5 Storage projections (~150 B/row raw, ~130 B/row candle)

| Scope | Raw (30-day window) | Daily candles / year |
|---|---|---|
| Commodities only (~12k items) | ~1.5 GB | ~0.6 GB/yr |
| + each allow-listed realm (gear, ~25k base items × ~2–4 variant factor) | ~+6–12 GB/realm | ~+2.5–5 GB/realm/yr |
| All ~240 US realms | ~720 GB+ | ~290 GB+/yr |

**Guidance:** start commodities-heavy with a **small allow-list (a handful of realms)**; **bump
Postgres storage before expanding** the allow-list. Variant granularity makes per-realm growth roughly
2–4× the base-item estimate for gear-heavy realms.

### 5.6 Storage tiers: hot Postgres + cold blob archive

Because Postgres compression is unavailable (§2.3), storage is split into tiers:

| Tier | Store | Contents | Retention |
|---|---|---|---|
| **Hot** | Postgres (Flexible Server) | raw hourly snapshots for the **allow-list** realms + commodities; daily candles | raw 30 d; candles indefinite |
| **Cold — raw** | Blob landing zone | gzipped raw Blizzard JSON per pull, all archive realms | configurable (e.g., 30–90 d), lifecycle-deleted |
| **Cold — archive** | Blob Parquet | compressed columnar hourly snapshots, **all archive realms** | indefinite, auto-tiered Cool→Cold→Archive |

- The **archive set** (what goes to blob) can be **broader than the hot allow-list** (default: all US
  realms), so Coinwarden captures *all item data* cheaply while Postgres stays lean — directly solving
  the old app's "couldn't store all items" limitation.
- **Parquet + Zstd** compresses this numeric series ~10–20× vs. Postgres heap+indexes, recovering most
  of the benefit lost by not having TimescaleDB compression.
- Blob is the **system of record for raw data**: if aggregation logic changes (MarketPrice formula,
  finer variant handling), reprocess from the landing zone — no data lost.
- Blob is **not** on the low-latency API path; the hot Postgres tier serves all read APIs (§7). Cold
  analytics use DuckDB / Synapse serverless over Parquet (§12).

---

## 6. Sync Jobs (Hangfire)

All jobs: extension-driven registration (matches template startup), per-job `Enabled`+`Schedule`
settings, `DisableConcurrentExecution`, correlation-id logging, `CancellationToken`, bounded retries.

### 6.1 `PullCommodityAuctionDataJob` — hourly

1. Conditional GET commodities (`If-Modified-Since`); `304` ⇒ done.
2. Stream-parse auctions; aggregate per **item** (base variant) into `AuctionSnapshot` rows with
   `ConnectedRealmId = 0` and `SnapshotHour = now` truncated to the hour.
3. Bulk-load via staging + merge (§6.7).
4. Collect **new item ids** → enqueue `SyncItemMetadataJob`.
5. Persist new `Last-Modified`.

### 6.2 `PullRealmAuctionDataJob` — hourly

1. Resolve realms: the **hot set** = union of subscription providers (§4.7); the **archive set**
   (≥ hot set, default all US realms) governs what is written to blob (§6.8).
2. For each realm in the archive set (queue with bounded re-tries, like the old app):
   - Conditional GET realm auctions; `304` ⇒ skip. Stream the gzipped raw JSON to the blob landing
     zone (§6.8).
   - Stream-parse; for each auction compute the **variant signature/hash** (§4.3); aggregate per
     `(item, variant)`; filter to the realm's item selector (all items, or class-filtered).
   - If the realm is in the **hot set**, bulk-load snapshots into Postgres; always retain the hourly
     aggregates for the Parquet archive. Collect **new items and new variants**.
3. Enqueue `SyncItemMetadataJob` for new items; upsert new `ItemVariant` rows.

### 6.3 `SyncItemMetadataJob` — on demand / periodic sweep

Preserves the old item-syncing flow (this is how item data is obtained):

1. Select items with `MetadataSyncedAt IS NULL` (or stale).
2. Batch via Blizzard item search (**≤100 ids/request**, parallel-chunked as the old app did).
3. Upsert `Item` rows; set `MetadataSyncedAt`. Ensure the WoW Token pseudo-item exists (port
   `EnsureWoWTokenItemExists`).
4. `ItemVariant` rows are created during pulls; this job may later enrich them (resolve bonus → item
   level) — see §12.

### 6.4 `SyncRealmTopologyJob` — daily

Port `PullRealmDataBackgroundJob`: fetch all connected realms (+ realms), upsert `ConnectedRealm`/
`Realm` (population, names, slugs), prune realms that disappeared. Seeds the allow-list's valid ids.

### 6.5 `RollupDailyCandlesJob` — daily

Aggregate the previous UTC day's snapshots → `auction_daily_candles` (§5.3). Runs **before** purge so
no data is lost. Idempotent.

### 6.6 `PartitionMaintenanceJob` — daily

- Pre-create upcoming **daily** `auction_snapshots` partitions (next few days) and the next **monthly**
  `auction_daily_candles` partition.
- `DROP` `auction_snapshots` partitions older than 30 days (§5.2).
- Self-healing: ensures the current period's partition always exists before writers run.

### 6.7 Bulk-insert pattern (write throughput)

Hourly writers can produce millions of rows. EF `AddRange`/`SaveChanges` is too slow. Pattern:

1. `COPY` aggregated rows into an **unlogged staging table** via Npgsql binary import
   (`BeginBinaryImport`).
2. `INSERT INTO auction_snapshots SELECT … FROM staging ON CONFLICT (…) DO UPDATE` (idempotent merge),
   then `TRUNCATE` staging.

Change tracking is disabled on the bulk path; aggregation happens in-app from the stream, the DB does
the merge. A small `IBulkAuctionWriter` abstraction wraps this.

### 6.8 `ArchiveAuctionDataToBlobJob` — write-through + daily

Writes to the **blob cold tier** (§5.6) via two paths:

- **Raw landing zone (write-through, during pulls):** the gzipped raw Blizzard JSON for every fetched
  source is streamed to `raw/{region}/{source}/{yyyy}/{MM}/{dd}/{HH}.json.gz` (`source` = `commodities`
  or `realm-{id}`). Cheap insurance enabling **replay/reprocessing** if aggregation logic changes.
- **Parquet archive (daily batch):** consolidates the day's aggregated hourly snapshots into compressed
  **Parquet** partitioned by `region/date` with one file per realm-day (large files, low object count).
  Covers the **archive set** (default: all US realms) — hot realms sourced from Postgres, archive-only
  realms from the pull's retained hourly aggregates. This is how **"capture all item data"** is met
  cheaply without bloating Postgres.

Idempotent (re-writing a day overwrites its objects). Uses `Azure.Storage.Blobs` + `Parquet.Net`. Blob
**lifecycle management** (§8) auto-tiers objects Hot→Cool→Cold→Archive by age and deletes the raw
landing zone after a configurable window. A cold query path (DuckDB / Synapse serverless) is future
work (§12).

### 6.9 Cadence summary

| Job | Default schedule |
|---|---|
| `PullCommodityAuctionDataJob` | hourly (e.g., `5 * * * *`) |
| `PullRealmAuctionDataJob` | hourly (e.g., `10 * * * *`) |
| `SyncItemMetadataJob` | every 15 min sweep + enqueued on discovery |
| `SyncRealmTopologyJob` | daily |
| `ArchiveAuctionDataToBlobJob` | raw write-through during pulls; Parquet daily after rollup |
| `RollupDailyCandlesJob` | daily, shortly after 00:00 UTC |
| `PartitionMaintenanceJob` | daily, before rollup/after purge window |

---

## 7. Read APIs (`/api/v1`)

All endpoints follow template conventions: versioned routes, `ServiceControllerBase`, services return
`Result<T>` mapped via `HandleServiceFailureResult`, **cursor pagination** (`CursorPaginatedList<T>` /
`ToCursorPaginatedResponse`), `ProblemDetailsWithErrors` + `X-Correlation-Id`, repositories extend
`Repository<…>`. Prices are returned as **copper `long`** (UI formats gold/silver/copper).

> **Auth posture:** the template applies global auth; market-data GETs are strong candidates for
> `[AllowAnonymous]` (public read). This spec **defaults to authenticated** and flags the open-read
> decision for the API/auth spec.

### 7.1 Items & realms

| Endpoint | Description |
|---|---|
| `GET /items` | search/list items — filters `nameContains`, `itemClass`, `quality`, `isCommodity`; cursor-paginated |
| `GET /items/{itemId}` | item detail |
| `GET /items/{itemId}/variants` | known variants for an item |
| `GET /connected-realms` | list connected realms (+ realms) |
| `GET /realms` | list/search realms |

### 7.2 Auction data (incl. aggregates)

| Endpoint | Description |
|---|---|
| `GET /auctions/snapshots` | **raw hourly** series. Required: `itemId`, `connectedRealmId` (`0`=commodities), `startDate`. Optional: `variantHash` (omit ⇒ aggregate across variants), `endDate`, cursor. |
| `GET /auctions/candles` | **daily candlesticks** (OHLC). Same filters, wider default window; primary charting source for ranges > 30 days. |
| `GET /auctions/latest` | latest snapshot for an item(+variant) on a realm, or across realms (current price + cross-realm comparison). |

Query parameters use a `CursorPaginationQueryParameters`-derived record (port
`AuctionTimeSeriesQueryParameters`) with validation. The repository translates `connectedRealmId` to
include the commodity sentinel when appropriate. DTOs: `AuctionSnapshotDto`, `AuctionDailyCandleDto`,
`ItemDto`, `ItemVariantDto`, `ConnectedRealmDto`, `RealmDto`.

### 7.3 Variant handling in reads

`variantHash` is an opaque base64-url token (consistent with the template's cursor encoding). Omitting
it returns a per-timestamp **aggregate across all variants** (e.g., min over variants, summed quantity)
so callers that don't care about variants get a clean series; supplying it pins to one physical item.

---

## 8. Configuration & Settings

`appsettings.json` (overridable per environment; secrets via Key Vault in non-Dev, per template):

```jsonc
{
  "Blizzard": {
    "ApiBaseUrl": "https://us.api.blizzard.com",
    "OAuthUrl": "https://oauth.battle.net/token",
    "Region": "us", "Namespace": "dynamic-us", "StaticNamespace": "static-us", "Locale": "en_US",
    "ClientId": "<key-vault>", "ClientSecret": "<key-vault>"
  },
  "AuctionPipeline": {
    "RawSnapshotRetentionDays": 30,
    "AllowListConnectedRealmIds": [],          // Postgres HOT-tier realms
    "AlwaysProcessItemClasses": { },           // { "Consumable": ["Potion","Flask"], ... }
    "MaxRealmRetryAttempts": 5,
    "Archive": {
      "Enabled": true,
      "ArchiveAllRealms": true,                // false ⇒ archive only the hot allow-list
      "ArchiveConnectedRealmIds": [],          // explicit set used when ArchiveAllRealms = false
      "RawLandingRetentionDays": 30            // lifecycle delete window for raw JSON
    }
  },
  "Storage": {
    "BlobAccountUrl": "https://<account>.blob.core.windows.net",  // Managed Identity (DefaultAzureCredential)
    "RawContainer": "auction-raw",
    "ArchiveContainer": "auction-archive"
  },
  "BackgroundJobs": {
    "PullCommodityAuctionData": { "Enabled": true, "Schedule": "5 * * * *" },
    "PullRealmAuctionData":     { "Enabled": true, "Schedule": "10 * * * *" },
    "SyncItemMetadata":         { "Enabled": true, "Schedule": "*/15 * * * *" },
    "SyncRealmTopology":        { "Enabled": true, "Schedule": "30 3 * * *" },
    "ArchiveAuctionDataToBlob": { "Enabled": true, "Schedule": "40 0 * * *" },
    "RollupDailyCandles":       { "Enabled": true, "Schedule": "20 0 * * *" },
    "PartitionMaintenance":     { "Enabled": true, "Schedule": "0 0 * * *" }
  }
}
```

Each job's `Enabled` flag gates `RecurringJob.AddOrUpdate` vs `RemoveIfExists` (port old behavior).

---

## 9. Starter-Template Alignment

- **Add Hangfire** (`Hangfire.AspNetCore` + `Hangfire.PostgreSql`, storage in the same Postgres) via
  new `ServiceCollectionExtensions`/`ApplicationBuilderExtensions` files (template's extension-driven
  startup). Secure the dashboard (authenticated/admin only).
- **Drop** old infra not used here: Azure Event Grid hooks (replace with a simple post-pull
  `IAuctionPullCompletedHandler` no-op interface), `Hangfire.JobsLogger`, MySQL/Pomelo remnants
  (we are Postgres).
- **C# style** per `.editorconfig`/`AGENTS.md`: `this.`-qualified members, `camelCase` fields (no
  underscore), `I`-prefixed interfaces, XML docs on public members, non-entity POCOs as `record` with
  `get; init;`, repositories extend `Repository<…>`/`IRepository<…>`, all async methods take/pass
  `CancellationToken`.
- **EF/Npgsql**: `AddDbContextPool`; bulk path uses a raw `NpgsqlConnection`.
- **Blob storage**: add a **Storage Account** Bicep module (`CI/Azure/modules/storage.bicep`) with two
  containers + a **lifecycle policy** (Hot→Cool→Cold→Archive; delete raw after N days). Access via
  `Azure.Storage.Blobs` with **Managed Identity** (`DefaultAzureCredential`) — no keys in config;
  grant the App Service the **Storage Blob Data Contributor** role. Parquet via `Parquet.Net`.
- **UI** (future, out of scope): **shadcn** components consuming `/auctions/candles` + `/snapshots`.

---

## 10. Performance & Operations

- **Conditional GETs** skip unchanged realms (`304`) — biggest CPU/IO saver.
- **Streaming JSON** + **`COPY` staging-merge** for write throughput; minimal indexing on fact tables.
- **Partition `DROP`** for retention (no `DELETE`/vacuum churn).
- **Set-based rollup** runs entirely in Postgres.
- **Rate budget:** 1 commodities + N realm + occasional 100-id item batches per hour ≪ 36k/hr.
- **Storage sizing:** start small (commodities + a few realms ≈ well under the 32 GB default); raise
  `storageSizeGB` in `postgres.bicep` before growing the hot allow-list (see §5.5). Offload the long
  tail to blob (§5.6) instead of over-provisioning Postgres. Expected spend in **§11**.
- **Connection pooling** via `AddDbContextPool`; bulk writer uses its own connection.
- **Blob ops:** write few large files (one Parquet per realm-day) to keep transaction costs negligible;
  rely on lifecycle tiering; keep all processing **in-region** (free transfer) to avoid internet egress.

---

## 11. Cost & Pricing Estimates

> Approximate **US region, LRS, pay-as-you-go, early-2026 list prices** — Azure prices drift and vary
> by region/redundancy; validate with the Azure Pricing Calculator. Excludes shared template costs
> (App Service, Key Vault, App Insights) and any free credits.

### 11.1 Postgres (Flexible Server) — the dominant cost

| Component | Rate | Notes |
|---|---|---|
| Compute B1ms (1 vCore / 2 GiB) | **~$12–15/mo** | template default; fine for commodities + a few realms |
| Compute B2ms (2 vCore / 4 GiB) | **~$25–30/mo** | headroom for hourly bulk-insert + rollup |
| Storage (provisioned) | **~$0.10–0.12/GB/mo** | grow in steps; backups included ≤100% of storage |

Hot-tier storage = raw 30-day window + indefinite daily candles + Hangfire + index/bloat headroom:

| Hot scope | Provisioned | Storage $/mo | Compute | **≈ Total/mo** |
|---|---|---|---|---|
| Commodities only | 32 GB | ~$3–4 | B1ms | **~$16–18** |
| + ~5 allow-list realms | 64–128 GB | ~$7–15 | B1ms–B2ms | **~$20–45** |
| + ~20 allow-list realms | 256 GB | ~$26–31 | B2ms | **~$55–61** |

Daily candles accrue forever (~0.6 GB/yr commodities + ~2.5–5 GB/realm/yr), so re-provision storage
every so often.

### 11.2 Blob storage — the cheap long tail

Storage (LRS): **Hot ~$0.018–0.023, Cool ~$0.010, Cold ~$0.0036, Archive ~$0.001–0.002 /GB/mo.**
Writes ~$0.05 (Hot) / $0.10 (Cool) / $0.15 (Cold/Archive) per 10k; reads cheap except Archive
(rehydration). **In-region transfer to the API is free**; only internet egress (~$0.09/GB) costs.
Object counts stay low (a few large files, not millions of tiny ones).

| Archive scope | Steady-state stored | Tier | Storage $/mo | Writes $/mo |
|---|---|---|---|---|
| Commodities + ~5 realms | ~20–40 GB | Cool | **<$1** | ~$0.10 |
| Raw landing, **all 240 realms**, 30-day lifecycle | ~150–300 GB | Hot→Cool | **~$2–4** | ~$1–2 |
| Parquet archive, **all 240 realms**, kept ~1 yr | ~1–2 TB (accruing) | Cold | **~$4–9** | ~$1–2 |
| …auto-tiered to Archive after 90 d | ~1–2 TB | Archive | **~$2–5** | ~$1–2 |

### 11.3 Bottom line

- **Realistic starter** (B1ms, commodities + a few hot realms, blob archiving the same):
  **~$17–25/mo all-in** for DB + storage.
- **"Capture everything"** (same hot tier, blob archiving **all 240 realms** raw + Parquet, long-term):
  adds only **~$5–15/mo** — blob makes full-fidelity capture affordable.
- **Compute dominates**; storage choices move the total by single-digit dollars until the hot
  allow-list grows large. Levers: keep the hot allow-list small, shrink the Postgres raw window to
  offload sooner, and lean on blob lifecycle tiering.

---

## 12. Open Questions & Future Work

- **Variant enrichment:** resolving `bonus_lists` → effective item level/affixes has no single Blizzard
  endpoint; needs community data or heuristics. v1 stores raw bonuses only.
- **Watch-list/alert subscription provider:** interface defined here; wiring lands with those features.
- **Multi-region:** model carries `Region`; enabling EU/KR/TW means region-scoped commodities + realm
  ids and larger storage.
- **Cold query path:** query the Parquet archive (§6.8) in place with **DuckDB** (embedded, free) or
  **Synapse serverless** for rare deep-history/backfill — not on the low-latency API path.
- **Compression alternatives:** the blob Parquet archive is the chosen substitute for in-Postgres
  compression (a self-managed VM with TSL TimescaleDB was rejected — only ~50 GB available). If hot-tier
  pressure grows: Timescale Cloud, or coarser **weekly/monthly rollups** beyond daily.
- **`pg_cron`** could run partition maintenance/rollup in-DB instead of Hangfire (deferred; Hangfire
  keeps it in one place).
- **MarketPrice definition** (cheapest-15%-weighted) should be validated against TSM-style expectations
  once charts exist.

---

## 13. Migration & Rollout

1. EF migration creates dimension tables (`Item`, `ItemVariant`, `ConnectedRealm`, `Realm`) + Hangfire
   schema.
2. Raw-SQL migration creates the **partitioned parent** fact tables + indexes (EF can't model these).
3. First deploy runs `SyncRealmTopologyJob` to seed realms; operator sets `AllowListConnectedRealmIds`
   (hot tier) and the `Archive` set.
4. Provision the **Storage Account** + containers + lifecycle policy (Bicep); grant the App Service
   **Storage Blob Data Contributor**.
5. `PartitionMaintenanceJob` bootstraps the initial day/month partitions before the first pull.
6. Pull jobs populate snapshots + blob; rollup/archive/purge engage on their daily cadence.

---

## Appendix A — Entity relationship summary

```
Item (1) ──< ItemVariant (N)          ConnectedRealm (1) ──< Realm (N)
   │                │
   └──────< AuctionSnapshot >─────────────────┘     (ConnectedRealmId = 0 ⇒ region commodities)
   └──────< AuctionDailyCandle >──────────────┘
ItemVariant identified on facts by denormalized VariantHash (base variant for commodities).
```

## Appendix B — Snapshot aggregation (per item/variant/realm/hour)

```
unitPrice(auction) = isCommodity ? unit_price : (buyout ?? bid) / max(quantity,1)
Quantity      = Σ quantity
NumAuctions   = count
MinUnitPrice  = min(unitPrice);  MaxUnitPrice = max(unitPrice)
AvgUnitPrice  = Σ(unitPrice*quantity) / Σ quantity
P{25,50,75,95}= quantity-weighted percentile (sort by price, walk cumulative quantity)
MarketPrice   = qty-weighted mean of the cheapest 15% of supply
```
