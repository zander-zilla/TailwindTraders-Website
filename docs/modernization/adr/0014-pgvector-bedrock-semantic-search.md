# 0014. Semantic product search with pgvector + Amazon Bedrock (offline provider for local)

- **Status:** Accepted (implemented in Phase 8. Local/kind/CI use the offline provider; the Bedrock path is written, not yet run against AWS)
- **Date:** 2026-10-10

## Context
- Search was a SQL `ILIKE '%term%'` on product names, used only by image search. Shoppers type intent ("something to water plants"), not exact names.
- The demo needs an AI feature that is useful, cheap, and runs on AWS without a new datastore.
- It must also run on a laptop, in kind and in CI **without AWS credentials**.

## Decision
- **Vector store:** the existing **PostgreSQL** with **pgvector** (Aurora PostgreSQL supports it; locally `pgvector/pgvector:pg17`, multi-arch).
  - EF Core mapping via `Pgvector.EntityFrameworkCore`.
  - New table `product_embeddings(product_id PK/FK, model, content_hash, embedding vector(1024), updated_at)`.
  - **HNSW** index with `vector_cosine_ops`. The migration enables the extension.
  - Vectors live in their own table, not on `products`, so switching models is a re-embed, not a schema change.
- **Embedding provider abstraction** (`IEmbeddingProvider`), selected by `Ai:EmbeddingProvider`:
  - `bedrock`: **Amazon Bedrock Titan Text Embeddings v2** (1024 dims, normalised). Credentials come from the AWS SDK default chain, which is **EKS Pod Identity** on AWS.
  - `local` (default): `local-hash-v1`, a deterministic, offline feature-hashing embedding (words, bigrams, character trigrams).
    - It needs no model download or network and copes with typos and plurals, but it is **lexical, not semantic**.
    - Chosen over a small ONNX sentence model to keep the image and build simple, and because its 1024 dims fit the same column.
- **Embeddings are generated in the `--migrate` step** (compose `migrate` service, Helm pre-upgrade Job).
  - Idempotent: only rows that are missing, from another model, or whose product text hash changed.
  - Uses bounded parallelism (4) for Bedrock rate limits.
  - Queries only compare vectors of the **same model**.
- **API:** `GET /api/v1/products/search?q=&take=` returns `{ query, model, results: [{ id, name, price, imageUrl, typeCode, typeName, brandName, score }] }`.
  - Results are ordered by cosine similarity, and hits below `Ai:MinScore` are dropped (0.2 local, 0.15 suggested for Bedrock).
  - The React header gets a search box and a `/search` results page that shows "% match".
- **Optional assistant** (`POST /api/v1/assistant`, flag `Ai:Assistant:Enabled`, **off by default**, 404 when off): RAG over the top-5 search hits.
  - Generation uses Bedrock **Converse** (`amazon.nova-lite-v1:0` by default), with a system prompt restricted to the retrieved catalog.
  - Without Bedrock it returns an offline templated answer, so the flow is demoable locally.
- **Observability:**
  - Spans `product.semantic_search` → `embeddings <model>` with OpenTelemetry GenAI attributes (`gen_ai.operation.name`, `gen_ai.system`, `gen_ai.request.model`, `gen_ai.usage.input_tokens`), plus the AWS SDK span for the Bedrock call and the Npgsql span for the `<=>` query.
  - The assistant adds `chat <model>`.
  - Metrics `tailwindtraders.embedding.duration` and `tailwindtraders.semantic_search.requests{result}`.
- **IAM / Terraform:** the app Pod Identity role gets `bedrock:InvokeModel` on exactly the embedding and chat models (foundation-model plus inference-profile ARNs). `helm_values` and `values-eks.yaml` set `Ai__EmbeddingProvider=bedrock`.

## Consequences
- Plus: no new datastore. Vectors sit next to the catalog in a transaction, and backups and IaC are unchanged. It's one `ORDER BY embedding <=> @q LIMIT k` query.
- Plus: works offline (compose, kind, CI, tests) and switches to Bedrock with env vars only.
- Minus: the local provider is lexical. "something to cut wood" finds "Wood Pack" rather than the saw, while Bedrock finds the saw. The docs say so; use Bedrock to show real semantics.
- Minus: Bedrock needs **model access enabled** in the account/region, and the migrate Job calls Bedrock (about 70 products → about 70 small calls per model change, a fraction of a cent).
- Minus: Aurora needs `CREATE EXTENSION vector`. The migration runs it as the master user, which has `rds_superuser`. A least-privilege app user would need the extension pre-created.
- Minus: HNSW is approximate. On 70 rows it's effectively exact, but recall/latency tuning (`ef_search`) matters at scale.
- Minus: the assistant sends user questions and catalog snippets to Bedrock, which is why it's off by default. Bedrock does not use the data for training; this is still a data-handling decision for the customer.
