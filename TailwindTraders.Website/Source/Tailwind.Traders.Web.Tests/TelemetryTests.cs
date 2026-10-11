using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace Tailwind.Traders.Web.Tests
{
    /// <summary>Phase 6: OpenTelemetry is wired: providers registered, app spans join the incoming W3C trace,
    /// child spans exist for PostgreSQL (Npgsql) and the AWS SDK (DynamoDB), custom metrics are recorded.</summary>
    [Collection("standalone")]
    public class TelemetryTests
    {
        private readonly StandaloneFixture _fx;
        public TelemetryTests(StandaloneFixture fx) => _fx = fx;

        private static StringContent Body(string json) => new(json, Encoding.UTF8, "application/json");

        [Fact]
        public void Tracer_and_meter_providers_are_registered()
        {
            Assert.NotNull(_fx.Factory.Services.GetService<TracerProvider>());
            Assert.NotNull(_fx.Factory.Services.GetService<MeterProvider>());
        }

        [Fact]
        public async Task Cart_add_emits_spans_in_the_callers_trace_and_metrics()
        {
            var traceId = ActivityTraceId.CreateRandom();
            var spans = new ConcurrentBag<Activity>();
            using var listener = new ActivityListener
            {
                ShouldListenTo = s => true,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = a => { if (a.TraceId == traceId) spans.Add(a); },
            };
            ActivitySource.AddActivityListener(listener);

            var measurements = new ConcurrentBag<(string name, long value, string op)>();
            using var meterListener = new MeterListener();
            meterListener.InstrumentPublished = (i, l) => { if (i.Meter.Name == AppTelemetry.Name) l.EnableMeasurementEvents(i); };
            meterListener.SetMeasurementEventCallback<long>((i, v, tags, _) =>
                measurements.Add((i.Name, v, tags.ToArray().FirstOrDefault(t => t.Key == "operation").Value as string)));
            meterListener.Start();

            var c = _fx.Factory.CreateClient();
            var login = await c.PostAsync("/api/v1/login", Body("{\"username\":\"otel@contoso.com\",\"password\":\"x\",\"grant_type\":\"password\"}"));
            var token = JsonDocument.Parse(await login.Content.ReadAsStringAsync()).RootElement.GetProperty("access_token").GetProperty("token").GetString();

            var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/shoppingcart") { Content = Body("{\"detailProduct\":{\"id\":5},\"qty\":2}") };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Headers.Add("traceparent", $"00-{traceId}-{ActivitySpanId.CreateRandom()}-01"); // as the browser would send
            Assert.Equal(HttpStatusCode.Created, (await c.SendAsync(req)).StatusCode);

            // Spans stop asynchronously relative to the response in TestServer; give them a moment.
            for (var i = 0; i < 50 && !spans.Any(s => s.OperationName == "cart.add"); i++) await Task.Delay(100);

            var cartAdd = Assert.Single(spans, s => s.OperationName == "cart.add");
            Assert.Equal("ok", cartAdd.GetTagItem("tailwindtraders.cart.result"));
            Assert.Equal(5, cartAdd.GetTagItem("tailwindtraders.product.id"));
            Assert.Contains(spans, s => s.Source.Name == "Microsoft.AspNetCore" && s.Kind == ActivityKind.Server);       // incoming request joined the trace
            Assert.Contains(spans, s => s.Source.Name == "Npgsql");                                                      // catalog lookup (price)
            Assert.Contains(spans, s => s.Source.Name.StartsWith("Amazon") || s.Source.Name.StartsWith("AWSSDK"));      // DynamoDB UpdateItem
            Assert.Contains(spans, s => s.ParentSpanId == cartAdd.SpanId);                                               // DB/AWS calls nest under cart.add

            meterListener.RecordObservableInstruments();
            Assert.Contains(measurements, m => m.name == "tailwindtraders.cart.operations" && m.op == "add");
            Assert.Contains(measurements, m => m.name == "tailwindtraders.cart.units_added" && m.value == 2);
        }
    }
}
