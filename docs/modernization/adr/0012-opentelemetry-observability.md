# 0012. OpenTelemetry for traces, metrics and logs (Aspire dashboard locally, CloudWatch/X-Ray on AWS)

- **Status:** Accepted (implemented in Phase 6. Local and kind verified; EKS path written, not yet applied)
- **Date:** 2026-10-10

## Context
The legacy app used the Application Insights SDK (removed in Phase 1, ADR 0004), so after Phase 1 it had no telemetry beyond plain console logs. The target runs on EKS with Aurora and DynamoDB, so a request crosses browser → ALB → pod → PostgreSQL/DynamoDB. The demo needs to *show* that path, locally on a Mac and in AWS, without vendor lock-in in the code.

## Decision
- **OpenTelemetry .NET SDK** in the app (`Telemetry.cs`). Instrumentation for ASP.NET Core, HttpClient, **Npgsql** (all EF Core SQL), the **AWS SDK** (DynamoDB calls) and .NET runtime metrics.
- **Custom telemetry** (source and meter `TailwindTraders.Web`):
  - spans `cart.get/add/update/delete`, `image_search`, `image_search.classify`
  - metrics `tailwindtraders.cart.operations{operation,result}`, `tailwindtraders.cart.units_added`, `tailwindtraders.image_search.requests{result}`, `tailwindtraders.image_search.duration`
  - The user id/email is **not** recorded (PII).
- **Configuration only through standard `OTEL_*` env vars** (endpoint, protocol, per-signal endpoints, `OTEL_*_EXPORTER=none`, sampler, resource attributes). There is no exporter unless an endpoint is set.
- **Logs:** JSON to stdout with `TraceId`/`SpanId` scopes (for `kubectl logs` and Container Insights), plus OTLP logs where the backend accepts them (the Aspire dashboard).
- **Browser:** OpenTelemetry Web SDK, enabled only when `BrowserTelemetry:OtlpTracesEndpoint` is set. XHR/fetch spans add a `traceparent` header, so one click becomes one end-to-end trace.
- **Local backend:** the **.NET Aspire dashboard** container (compose and kind). It's a single image with no storage, multi-arch, and has an OTLP receiver and UI.
- **AWS backend:** the **`amazon-cloudwatch-observability` EKS add-on** (CloudWatch agent + Fluent Bit, with Pod Identity for CloudWatch and X-Ray).
  - The app exports OTLP/HTTP to `cloudwatch-agent.amazon-cloudwatch:4316`, which sends traces to **X-Ray / Application Signals** and metrics to CloudWatch. Logs travel stdout → Fluent Bit → CloudWatch Logs.
  - Head sampling is 25 % (`parentbased_traceidratio`).
  - Chosen over a separate ADOT collector because it's one managed add-on that also provides Container Insights. The OTLP config is the same, so swapping in ADOT later is an env-var change.
- **Resilience:** outbound HTTP (the optional external image predictor) uses `Microsoft.Extensions.Http.Resilience` `AddStandardResilienceHandler()` (timeouts, retry with jitter, circuit breaker). DynamoDB and Npgsql keep their built-in retries.

## Consequences
- Plus: vendor-neutral code. Changing the backend (Grafana/Tempo, Datadog, Honeycomb) is a matter of env vars.
- Plus: the demo shows browser → API → PostgreSQL/DynamoDB in one trace on a laptop.
- Plus: CI asserts that spans actually arrive (compose and kind jobs), and a unit test checks trace propagation and the custom metrics.
- Minus: around 10 new NuGet/npm packages, and the SPA bundle grows (OTel web SDK, tens of KB gzipped).
- Minus: the local dashboard is anonymous and in-memory (demo only), and browser tracing is off in AWS. CloudWatch RUM would be the AWS-native follow-up.
- Minus: X-Ray/CloudWatch cost scales with spans and metrics. Sampling and the metric set are tunable in values.
