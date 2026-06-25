# Technical Spec: Auction Pipeline — Future Enhancements

**Status:** Backlog / not scheduled for v1
**Owner:** @rob893
**Relates to:** [`auction-data-pipeline.md`](./auction-data-pipeline.md) (the v1 spec)

This document collects designed-but-deferred enhancements to the auction data pipeline. They are
**out of scope for v1** and intentionally removed from the pipeline spec to keep it shippable. The
designs are preserved here so the thinking isn't lost and can be picked up later.

---

## 1. Cold storage tiering (Azure Blob: raw landing zone + Parquet archive)

### 1.1 Why this was deferred

v1 stores everything in Postgres: a 30-day raw hourly window (partitioned) plus indefinite daily
candlesticks. That covers **every v1 read API** — nothing user-facing depends on blob storage.

The blob tier adds real implementation surface (a storage account, a Parquet writer, lifecycle
policies, Managed Identity + RBAC, an extra Hangfire job, and extra config) while providing **no v1
feature**: blob data is **not queryable from the app** without a separate analytical engine
(DuckDB/Synapse). So it was cut from v1.

**The one trade-off accepted by deferring:** the raw-JSON landing zone is *now-or-never* insurance.
Auction data is non-reproducible — once an hour passes, that market snapshot is gone. Without the
landing zone, v1 keeps only **aggregated** snapshots, so history captured during the v1 window can
**never be retroactively reprocessed** (e.g., if the `MarketPrice` formula or variant handling changes
later). The old app never had this capability either, and the landing zone can be added later — it
just won't have backfilled the v1 window. Acceptable for a hobby project.

### 1.2 Motivation (when to build this)

Build the cold tier when **any** of these become true:

- You want to **capture all ~240 US realms** (not just the hot allow-list) cheaply — the original
  "couldn't store all item data" pain point.
- Postgres storage cost/pressure grows and you want to offload the long tail.
- You want **replay/reprocessing** insurance against aggregation-logic changes.
- You add multi-region and the per-region raw volume balloons.

The driving constraint: **Azure Postgres Flexible Server's TimescaleDB is the Apache-2 build → no
native compression.** Blob + Parquet recovers that compression externally.

### 1.3 Tiered model

| Tier               | Store                      | Contents                                                                        | Retention                                       |
| ------------------ | -------------------------- | ------------------------------------------------------------------------------- | ----------------------------------------------- |
| **Hot**            | Postgres (Flexible Server) | raw hourly snapshots for the **allow-list** realms + commodities; daily candles | raw 30 d; candles indefinite                    |
| **Cold — raw**     | Blob landing zone          | gzipped raw Blizzard JSON per pull, all archive realms                          | configurable (e.g., 30–90 d), lifecycle-deleted |
| **Cold — archive** | Blob Parquet               | compressed columnar hourly snapshots, **all archive realms**                    | indefinite, auto-tiered Cool→Cold→Archive       |

- The **archive set** (what goes to blob) can be **broader than the hot allow-list** (default: all US
  realms), so Coinwarden captures _all item data_ cheaply while Postgres stays lean.
- **Parquet + Zstd** compresses this numeric series ~10–20× vs. Postgres heap+indexes, recovering most
  of the benefit lost by not having TimescaleDB compression.
- Blob becomes the **system of record for raw data**: if aggregation logic changes, reprocess from the
  landing zone — no data lost.
- Blob is **not** on the low-latency API path; the hot Postgres tier still serves all read APIs. Cold
  analytics use DuckDB / Synapse serverless over Parquet (§1.7).

### 1.4 `ArchiveAuctionDataToBlobJob` (Hangfire) + pull-job changes

Writes to the blob cold tier via two paths:

- **Raw landing zone (write-through, during pulls):** the gzipped raw Blizzard JSON for every fetched
  source is streamed to `raw/{region}/{source}/{yyyy}/{MM}/{dd}/{HH}.json.gz` (`source` = `commodities`
  or `realm-{id}`). Cheap insurance enabling **replay/reprocessing**.
- **Parquet archive (daily batch):** consolidates the day's aggregated hourly snapshots into compressed
  **Parquet** partitioned by `region/date` with one file per realm-day (large files, low object count).
  Covers the **archive set** (default: all US realms) — hot realms sourced from Postgres, archive-only
  realms from the pull's retained hourly aggregates. This is how **"capture all item data"** is met
  cheaply without bloating Postgres.

Idempotent (re-writing a day overwrites its objects). Uses `Azure.Storage.Blobs` + `Parquet.Net`. Blob
**lifecycle management** auto-tiers objects Hot→Cool→Cold→Archive by age and deletes the raw landing
zone after a configurable window.

**Required change to `PullRealmAuctionDataJob`** (vs. the v1 Postgres-only version): split the target
realms into a **hot set** (= union of subscription providers, written to Postgres) and a broader
**archive set** (≥ hot set, default all US realms). During each realm pull: stream the gzipped raw JSON
to the landing zone; parse + aggregate per `(item, variant)`; if the realm is in the hot set, bulk-load
snapshots into Postgres; always retain the hourly aggregates so the daily Parquet job can archive
archive-only realms.

Cadence: raw write-through happens during the hourly pulls; the Parquet batch runs daily after the
rollup (e.g., `40 0 * * *`).

### 1.5 Configuration additions

```jsonc
{
  "AuctionPipeline": {
    "Archive": {
      "Enabled": true,
      "ArchiveAllRealms": true,        // false ⇒ archive only the hot allow-list
      "ArchiveConnectedRealmIds": [],  // explicit set used when ArchiveAllRealms = false
      "RawLandingRetentionDays": 30    // lifecycle delete window for raw JSON
    }
  },
  "Storage": {
    "BlobAccountUrl": "https://<account>.blob.core.windows.net", // Managed Identity (DefaultAzureCredential)
    "RawContainer": "auction-raw",
    "ArchiveContainer": "auction-archive"
  },
  "BackgroundJobs": {
    "ArchiveAuctionDataToBlob": { "Enabled": true, "Schedule": "40 0 * * *" }
  }
}
```

### 1.6 Infrastructure & alignment

- Add a **Storage Account** Bicep module (`CI/Azure/modules/storage.bicep`) with two containers
  (`auction-raw`, `auction-archive`) + a **lifecycle policy** (Hot→Cool→Cold→Archive; delete raw after
  N days).
- Access via `Azure.Storage.Blobs` with **Managed Identity** (`DefaultAzureCredential`) — no keys in
  config; grant the App Service the **Storage Blob Data Contributor** role.
- Parquet via `Parquet.Net`.
- **Ops:** write few large files (one Parquet per realm-day) to keep transaction costs negligible; rely
  on lifecycle tiering; keep all processing **in-region** (free transfer) to avoid internet egress.

### 1.7 Cold query path

Blob data is not directly usable by the API. To query the Parquet archive for rare deep-history or
backfill work, use **DuckDB** (embedded, free) or **Synapse serverless** over the Parquet files — never
on the low-latency API path. Surfacing cold data to users would require reprocessing it back into
Postgres.

### 1.8 Pricing (approximate, US, LRS, pay-as-you-go, early-2026 list)

Blob storage (LRS): **Hot ~$0.018–0.023, Cool ~$0.010, Cold ~$0.0036, Archive ~$0.001–0.002 /GB/mo.**
Writes ~$0.05 (Hot) / $0.10 (Cool) / $0.15 (Cold/Archive) per 10k; reads cheap except Archive
(rehydration). **In-region transfer to the API is free**; only internet egress (~$0.09/GB) costs.
Object counts stay low (a few large files, not millions of tiny ones).

| Archive scope                                     | Steady-state stored | Tier     | Storage $/mo | Writes $/mo |
| ------------------------------------------------- | ------------------- | -------- | ------------ | ----------- |
| Commodities + ~5 realms                           | ~20–40 GB           | Cool     | **<$1**      | ~$0.10      |
| Raw landing, **all 240 realms**, 30-day lifecycle | ~150–300 GB         | Hot→Cool | **~$2–4**    | ~$1–2       |
| Parquet archive, **all 240 realms**, kept ~1 yr   | ~1–2 TB (accruing)  | Cold     | **~$4–9**    | ~$1–2       |
| …auto-tiered to Archive after 90 d                | ~1–2 TB             | Archive  | **~$2–5**    | ~$1–2       |

**Bottom line:** adding the blob tier (raw + Parquet for **all 240 realms**, long-term) costs only
**~$5–15/mo** on top of the v1 Postgres spend — blob makes full-fidelity capture affordable. Compute
still dominates total cost.

### 1.9 Migration steps (when adopting)

1. Provision the **Storage Account** + containers + lifecycle policy (Bicep); grant the App Service
   **Storage Blob Data Contributor**.
2. Add `Storage` + `AuctionPipeline.Archive` config and register `ArchiveAuctionDataToBlob`.
3. Extend `PullRealmAuctionDataJob` with the hot-set/archive-set split and raw write-through (§1.4).
4. Pull jobs begin writing the landing zone immediately; the daily Parquet job backfills from retained
   aggregates.

---

## 2. Other deferred items

- **Compression alternatives (beyond blob):** if hot-tier pressure grows even with the cold tier —
  **Timescale Cloud** (managed, includes compression), a self-managed Postgres VM with TSL TimescaleDB
  (rejected for v1: only ~50 GB available), or coarser **weekly/monthly rollups** beyond the daily
  candlesticks.

> Non-blob future work (variant enrichment, watch-list/alert subscription provider, multi-region,
> `pg_cron`, `MarketPrice` validation) remains tracked in the pipeline spec's "Open Questions & Future
> Work" section.
