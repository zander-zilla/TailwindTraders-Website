# 0005. Fix connection-string leak via `/api/settings`

- **Status:** Accepted (Phase 1, PR #2)
- **Date:** 2026-10-10

## Context
`/api/settings` returns the `Settings` object to the SPA. `SqlConnectionString` and `MongoConnectionString` were marked with **Newtonsoft** `[JsonIgnore]`, but ASP.NET Core (since 3.0) serializes with **System.Text.Json**, which ignores Newtonsoft attributes. As a result **database connection strings, including passwords, were sent to every browser.**
The same mismatch broke standalone login (`[JsonProperty("grant_type")]` never bound, so login always returned 400) and the cart item id (`[JsonProperty("_cdbid")]`, which the UI reads).

## Decision
- Replace all Newtonsoft attributes with System.Text.Json equivalents (`[JsonIgnore]`, `[JsonPropertyName]`). Remove the Newtonsoft usage.
- Add a regression test asserting `/api/settings` contains no `sqlConnectionString` / `mongoConnectionString`.
- Longer term, secrets come from AWS Secrets Manager via External Secrets ([ADR 0009](0009-eks-and-helm.md)), and the SPA settings move to an explicit DTO (allow-list rather than deny-list).

## Consequences
- The leak is closed and guarded by tests. Login and cart update/delete work again.
- Any environment that ran the legacy app with real credentials should **rotate those database passwords**.
- The API JSON contract changes as intended: `access_token.token_type`, `expires_in` and cart `_cdbid` now match what the UI expects.
