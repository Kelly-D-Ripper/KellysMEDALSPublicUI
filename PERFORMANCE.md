# MEDALS panel performance validation — 19 September 2026

These measurements are a Windows desktop synthetic benchmark, not an in-game FPS
claim or a promise of zero lag. Runtime Unity UI/rendering, Harmony and network costs
still need in-game profiling with multiple viewers. No live game was restarted or
instrumented for this test.

The benchmark uses the actual 296-medal catalogue and a fresh synthetic indexed
SQLite database with 1,000 players, 94,000 progress rows and 20,000 awards. Each
measurement warms up first; individual operations have 200–300 samples.

| Operation | Median | 95th percentile |
| --- | ---: | ---: |
| Full catalogue compression/chunking, worker | 0.6120 ms | 0.7765 ms |
| Full catalogue decode, client | 0.2465 ms | 0.3977 ms |
| Compare unchanged catalogue and create keep-alive, worker | 0.0151 ms | 0.0161 ms |
| Former repeated filter/sort, client | 0.0346 ms | 0.0634 ms |
| Indexed snapshot including worker wake-up | 0.6954 ms | 0.9113 ms |
| 32 queued viewers, total worker completion latency | 22.95 ms | 28.33 ms |

An unchanged refresh falls from 13 packets / 10,706 payload characters to one packet
/ 80 characters: **99.25% less idle payload**, excluding transport framing. At 64
steady idle viewers and 12 refreshes per minute, this is approximately 8.22 MB down
to 61 KB per minute. Initial connections and actual changed stats still send a
complete snapshot. Traffic savings are workload-dependent, not a fixed FPS gain.

The list and text layout now rebuild only when received rows or user controls change.
Keep-alives preserve the existing row/list references and require no decompression.
Repeated fallback/native transform writes were removed. The idle freshness label
updates at most once per second. Only five row controls are displayed, and hidden
panels do not refresh their presentation or renew subscriptions.

Server work is bounded: four outgoing UI packets per Unity frame across all viewers,
round-robin delivery, two new reads per 100 ms globally, one pending per connection,
and optional reads rejected behind 32 queued authoritative items. Rejected reads do
not increment dropped-telemetry counters. Fair admission prevents early subscribers
from monopolising reads. SQL/compression remain on the ledger worker.

The regression suite exercises 64 simultaneous full replies and verifies the exact
per-frame cap, fair progress, completion, revoked/changed data, failed sends, nonce
and heartbeat replay handling, stale/resync behaviour, view invalidation and
authoritative-queue backpressure. All 2,320 combined MEDALS checks pass.

The publisher ran the database benchmark against synthetic data. Timings vary with hardware/runtime/load; the
queue and packet limits are deterministic. In-game acceptance should compare frame
times with MED closed/open, simultaneous viewers, incoming awards, page/scroll/map
interaction, reconnects and high telemetry traffic before declaring the preview stable.
