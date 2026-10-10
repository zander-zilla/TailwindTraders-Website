using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using SkiaSharp;
using Xunit;

namespace Tailwind.Traders.Web.Tests
{
    /// <summary>Smoke test for the embedded ONNX image search (exercises OnnxRuntime + ImageSharp after upgrades).</summary>
    [Collection("standalone")]
    public class ImageSearchSmokeTests
    {
        private readonly StandaloneFixture _fx;
        public ImageSearchSmokeTests(StandaloneFixture fx) => _fx = fx;

        [Fact]
        public async Task Image_classifier_runs_onnx_model_without_server_error()
        {
            using var bmp = new SKBitmap(64, 64);
            bmp.Erase(new SKColor(200, 120, 40));
            using var ms = new MemoryStream();
            using (var png = bmp.Encode(SKEncodedImageFormat.Png, 100)) { png.SaveTo(ms); }

            var content = new MultipartFormDataContent();
            var file = new ByteArrayContent(ms.ToArray());
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(file, "file", "test.png");

            var r = await _fx.Factory.CreateClient().PostAsync("/api/v1/products/imageclassifier", content);
            var body = await r.Content.ReadAsStringAsync();
            // 200 (matching products) or 404 ("No results found for <label>") both mean the model produced a label.
            Assert.True(r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.NotFound, $"{r.StatusCode}: {body}");
        }
    }
}
