# Efficiency and contract clarity iteration

This iteration addresses concrete repeated work and long argument lists. It does not change the approved-scope questions in the [production review](production-architecture-review.md), certify production readiness, or claim a measured end-to-end throughput gain.

## Changes and reasoning

- RangeInput, PhoneInput and ReservationInput are records with named init properties. JSON field names remain the same; call sites show the meaning of each value instead of relying on positional argument order.
- Domain construction takes NumberRangeDefinition or ReservationDetails, defined in Domain. Application maps request DTOs into them. Entities do not depend on HTTP request types. Domain guards still validate all inputs. Range boundary immutability now lives in the entity as well as being respected by the use case.
- API responses use RangeResponse, PhoneResponse and ReservationResponse. EF read queries project directly into these records, including single-record queries, so API reads do not populate the entity change tracker. Write use cases load entities and enforce rules transactionally, then return DTOs. Public JSON fields are preserved, including NumericValue/Version/Kind for compatibility. OpenAPI component/type names change to explicit response names; regenerate typed clients if they depend on generated schema names. Full response DTOs deliberately preserve existing fields, so this is not a claim of reduced API payload size.
- Synchronization validates/deduplicates the fetched assignments in one pass, then checks managed numbers in batches of 500. Unmanaged numbers cause no directory lookup or write transaction. Batch state is an optimization hint: write operations reload the entity inside their transaction.
- Already matching assignments with no reservation or scheduled release skip write transactions. Changed assignments still use the existing transactional path.
- User existence observations are reused only within one synchronization call, and only while less than 30 seconds old. Long runs refresh them. Errors are not cached; a clock reversal invalidates reuse. Cleanup performs its own fresh directory checks. There is no singleton/distributed cache of availability or release evidence. The short reuse window is a documented operational assumption that still needs production freshness approval.
- The offboarding scan queries only assigned numbers, selecting ID, user ID and numeric cursor. Numeric keyset pagination replaces growing OFFSET queries for the worker; external list endpoint pagination remains unchanged. Under concurrent inventory changes, numbers inserted/assigned behind the cursor are picked up by a later reconciliation run, not promised snapshot processing.
- Range generation parses the numeric end boundary once, rather than once per generated number.
- Worker configuration now treats missing, empty or whitespace-only Hangfire connection strings as a request to reuse Registry, matching the README. The actual PostgreSQL worker startup remains environment-dependent.

## Evidence and limits

EfficiencyTests uses relational SQLite queries and a transaction interceptor, plus counted Graph lookup calls. These are deterministic work-count tests, not a benchmark against production PostgreSQL or real network latency.

| Scenario | Previous code path | Tested new path |
|---|---|---|
| Three managed numbers for the same existing user during a short run | Up to six directory existence calls: one per assignment and one per assigned phone | One existence lookup reused within the window |
| Second sync of those unchanged assignments | Three per-assignment write transactions, even with no changed values | Zero write transactions |
| One unmanaged number | Directory lookup and a write transaction to discover it is unmanaged | Zero directory lookups and zero write transactions |
| Read-only range/phone/reservation responses | Entity instances, with by-ID reads tracked | Explicit DTOs, no tracked read entities |
| 601 assigned numbers | Offset pages over all numbers | Cursor crosses the 500-row boundary and reaches the final user |

Tests also cover cache refresh during a long run, separation between runs and cleanup, conflict validation before writes, and unchanged JSON field values for all three response types. Existing lifecycle, authorization, expiry, cascade and Graph tests remain in the suite.

Remaining scaling work must be driven by a representative PostgreSQL workload: inspect EXPLAIN ANALYZE and index selectivity for the global assigned-number cursor, count database/Graph calls, measure p95/p99 request latency, allocation/GC, lock waits, transaction aborts and complete-run age. The current range/status/numeric compound index does not automatically prove optimal global cursor performance. No speculative schema/index migration, tracking disablement on writes, DbContext pooling, parallel database operations on one context, or new runtime mapping/cache library was introduced.

The Graph adapter still buffers the complete external assignment scan, and the validation dictionary is O(N); batching local checks does not make external snapshot memory bounded. If tenant size requires streaming/staging, design that together with complete-scan and stale-observation semantics. PostgreSQL constraints and real multi-process concurrency/recovery tests from the production review remain required. Removed-assignment reconciliation, remote release evidence, audit/operations and poison-item cleanup are not fixed by this optimization pass.

## Type selection

EF entities remain classes because they have stable identity and mutable lifecycle. DTOs are record classes, and named parameter objects improve clarity rather than memory speed. Structs fit small value-semantic data only when measured copy/allocation behavior warrants them; turning large mutable entities or DTOs into structs is not an automatic optimization.

References: [Microsoft type selection](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/tutorials/choosing-types), [EF efficient querying](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying).
