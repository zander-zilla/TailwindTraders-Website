using SkiaSharp;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

namespace Tailwind.Traders.Web.Standalone.Services
{
    public class HttpEndpointSearchTermPredictor : IImageSearchTermPredictor
    {
        private readonly ILogger<HttpEndpointSearchTermPredictor> logger;
        private readonly string imageEndpoint;
        private readonly BlobServiceClient blobServiceClient;
        private readonly IHttpClientFactory httpClientFactory;
        public const string HttpClientName = "image-predictor";

        public HttpEndpointSearchTermPredictor(IConfiguration config, ILogger<HttpEndpointSearchTermPredictor> logger, IHttpClientFactory httpClientFactory)
        {
            this.logger = logger;
            this.httpClientFactory = httpClientFactory;
            this.imageEndpoint = config["ImagePredictorEndpoint"];
            try
            {
                blobServiceClient = new BlobServiceClient(config["StorageConnectionString"]);
            }
            catch (ArgumentException)
            {
                throw new ArgumentException("No 'StorageConnectionString' setting has been configured");
            }
        }

        async Task<string> IImageSearchTermPredictor.PredictSearchTerm(Stream imageStream)
        {
            var eventId = new EventId();
            using var image = SKBitmap.Decode(imageStream);

            // resize image constraining it to 500px in any dimension (keep aspect ratio, never upscale)
            var scale = Math.Min(1.0, Math.Min(500.0 / image.Width, 500.0 / image.Height));
            using var resizedImage = image.Resize(
                new SKImageInfo((int)Math.Round(image.Width * scale), (int)Math.Round(image.Height * scale)),
                new SKSamplingOptions(SKFilterMode.Linear));

            // upload the file
            var blobContainerClient = blobServiceClient.GetBlobContainerClient("website-uploads");
            var filename = Guid.NewGuid().ToString() + ".jpg";
            var cloudBlockBlob = blobContainerClient.GetBlockBlobClient(filename);
            var blobStream = new MemoryStream();
            using (var encoded = resizedImage.Encode(SKEncodedImageFormat.Jpeg, 90)) { encoded.SaveTo(blobStream); }
            blobStream.Seek(0,SeekOrigin.Begin);
            await cloudBlockBlob.UploadAsync(blobStream);

            logger.LogInformation(eventId, "Image uploaded to {StorageUrl}", cloudBlockBlob.Uri.AbsoluteUri);

            // pass the file to the endpoint
            var fullEndpoint = imageEndpoint + cloudBlockBlob.Uri.AbsoluteUri;
            // Named client: standard resilience pipeline (retry w/ jitter, circuit breaker, timeouts) + HttpClient tracing.
            var client = httpClientFactory.CreateClient(HttpClientName);
            var response = await client.GetAsync(fullEndpoint);
            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadAsStreamAsync();

            var serializer = new DataContractJsonSerializer(typeof(ImageRecognitionResult));
            var resultObject = (ImageRecognitionResult)serializer.ReadObject(responseBody);

            logger.LogInformation(eventId, "Result prediction: {prediction} with confidence - hammer: {hammerconf}, wrench: {wrenchconf}", 
                resultObject.prediction, resultObject.scores.hammer, resultObject.scores.wrench);

            return await Task.FromResult(resultObject.prediction);;
        }
    }

    [DataContract]
    public class ImageRecognitionResult {
        [DataMember]
        public string time { get; set; }
        [DataMember]
        public string prediction { get; set; }
        [DataMember]
        public PredictionScores scores { get; set; }
    }

    [DataContract]
    public class PredictionScores {
        [DataMember]
        public decimal hammer { get; set; }
        [DataMember]
        public decimal wrench { get; set; }
    }
}
