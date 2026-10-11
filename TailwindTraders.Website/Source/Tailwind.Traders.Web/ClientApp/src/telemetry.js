// Browser tracing (Phase 6). Enabled only when /api/settings returns browserTelemetry.otlpTracesEndpoint
// (e.g. the local Aspire dashboard). Page loads and every XHR/fetch to the API become spans, and
// same-origin requests carry a W3C `traceparent` header, so the backend trace (ASP.NET Core ->
// PostgreSQL / DynamoDB) continues the browser trace: one end-to-end trace per click.
import { WebTracerProvider } from "@opentelemetry/sdk-trace-web";
import { BatchSpanProcessor } from "@opentelemetry/sdk-trace-base";
import { OTLPTraceExporter } from "@opentelemetry/exporter-trace-otlp-http";
import { registerInstrumentations } from "@opentelemetry/instrumentation";
import { XMLHttpRequestInstrumentation } from "@opentelemetry/instrumentation-xml-http-request";
import { FetchInstrumentation } from "@opentelemetry/instrumentation-fetch";
import { DocumentLoadInstrumentation } from "@opentelemetry/instrumentation-document-load";
import { resourceFromAttributes } from "@opentelemetry/resources";
import { ATTR_SERVICE_NAME } from "@opentelemetry/semantic-conventions";

let started = false;

export function startBrowserTelemetry(settings) {
  const endpoint = settings && settings.otlpTracesEndpoint;
  if (started || !endpoint) return;
  started = true;
  try {
    const provider = new WebTracerProvider({
      resource: resourceFromAttributes({ [ATTR_SERVICE_NAME]: settings.serviceName || "tailwindtraders-spa" }),
      spanProcessors: [new BatchSpanProcessor(new OTLPTraceExporter({ url: endpoint }))],
    });
    provider.register(); // W3C trace-context propagator + default context manager

    const ignoreUrls = [new RegExp("^" + endpoint.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"))]; // don't trace the exporter
    registerInstrumentations({
      instrumentations: [
        new DocumentLoadInstrumentation(),
        new XMLHttpRequestInstrumentation({ ignoreUrls }), // axios uses XHR
        new FetchInstrumentation({ ignoreUrls }),
      ],
    });
  } catch (e) {
    // Telemetry must never break the shop.
    console.warn("Browser telemetry disabled:", e);
  }
}
