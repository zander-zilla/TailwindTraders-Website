using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace Tailwind.Traders.Web.Standalone.Services
{
    public class OnnxImageSearchTermPredictor : IImageSearchTermPredictor
    {
        private readonly ILogger<OnnxImageSearchTermPredictor> logger;
        private readonly InferenceSession session;

        public OnnxImageSearchTermPredictor(IWebHostEnvironment environment, ILogger<OnnxImageSearchTermPredictor> logger)
        {
            this.logger = logger;
            var filePath = Path.Combine(environment.ContentRootPath, "Standalone/OnnxModels/products.onnx");
            //var file = System.IO.File.ReadAllBytes(filePath);
            session = new InferenceSession(filePath);
        }

        public Task<string> PredictSearchTerm(Stream imageStream)
        {
            DenseTensor<float> data = ConvertImageToTensor(imageStream);
            var input = NamedOnnxValue.CreateFromTensor<float>("data", data);
            using var output = session.Run(new[] { input });
            var prediction = output.First(i => i.Name == "classLabel").AsEnumerable<string>().First();
            return Task.FromResult(prediction);
        }

        private DenseTensor<float> ConvertImageToTensor(Stream imageStream)
        {
            var data = new DenseTensor<float>(new[] { 1, 3, 224, 224 });
            // SkiaSharp (MIT) replaces ImageSharp: ImageSharp 3.x has open CVEs and 4.x requires a commercial license key.
            using var original = SKBitmap.Decode(imageStream);
            using var resized = original.Resize(new SKImageInfo(224, 224, SKColorType.Rgba8888), new SKSamplingOptions(SKFilterMode.Linear));
            for (var y = 0; y < resized.Height; y++)
            {
                for (var x = 0; x < resized.Width; x++)
                {
                    var color = resized.GetPixel(x, y);
                    data[0, 0, x, y] = color.Blue;
                    data[0, 1, x, y] = color.Green;
                    data[0, 2, x, y] = color.Red;
                }
            }
            return data;
        }
    }
}