# 0004. Remove Azure Personalizer and the Application Insights server SDK

- **Status:** Accepted (Phase 1, PR #2)
- **Date:** 2026-10-10

## Context
- **Azure AI Personalizer** has been retired. The app had a `PersonalizerController` (rank/reward) and the home page called `/api/personalizer/rank`.
- The **Application Insights** server SDK (2.16) was used for telemetry and Mongo dependency tracking. The `.csproj` hard-coded an Azure subscription id and App Insights resource ids. The target platform is AWS ([ADR 0006](0006-target-aws.md)), and the observability standard is OpenTelemetry.

## Decision
- Delete the Personalizer controller, settings and SDK. The home page keeps the default category order. AI recommendations return in Phase 8 (pgvector + Amazon Bedrock).
- Remove the App Insights server SDK, the Mongo telemetry hook, the `Connected Services` folder and the hard-coded subscription/resource ids.
- Add OpenTelemetry → ADOT → CloudWatch/X-Ray in Phase 6.

## Consequences
- No dead calls to a retired service, and no Azure identifiers in source.
- There is **no server telemetry between Phase 1 and Phase 6**, which is acceptable for a demo branch. The App Insights JS SDK and `ApplicationInsights` settings remain in the frontend until Phase 6/7.
