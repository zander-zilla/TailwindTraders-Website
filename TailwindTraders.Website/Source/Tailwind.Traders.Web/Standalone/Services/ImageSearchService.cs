using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tailwind.Traders.Web.Standalone.Models;

namespace Tailwind.Traders.Web.Standalone.Services
{
    public class ImageSearchService : IImageSearchService
    {
        private readonly IImageSearchTermPredictor predictor;
        private readonly IProductService productService;

        public ImageSearchService(
            IImageSearchTermPredictor predictor, IProductService productService)
        {
            this.predictor = predictor;
            this.productService = productService;
        }

        public async Task<ImageSearchResult> GetProducts(Stream imageStream)
        {
            using var activity = AppTelemetry.Source.StartActivity("image_search");
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            string outcome = "error";
            try
            {
            string searchTerm;
            using (var predict = AppTelemetry.Source.StartActivity("image_search.classify"))
            {
                predict?.SetTag("tailwindtraders.predictor", predictor.GetType().Name);
                searchTerm = await predictor.PredictSearchTerm(imageStream);
                predict?.SetTag("tailwindtraders.image_search.term", searchTerm);
            }
            activity?.SetTag("tailwindtraders.image_search.term", searchTerm);

            var result = new ImageSearchResult {
                PredictedSearchTerm = searchTerm
            };

            var products = await productService.GetProducts(searchTerm: searchTerm);

            var searchResults = products.Select(p => new SearchProductItem
            {
                Id = p.Id,
                ImageUrl = p.ImageUrl,
                Name = p.Name,
                Price = (float)p.Price
            });
            result.SearchResults = searchResults.ToList();
            var count = ((System.Collections.Generic.List<SearchProductItem>)result.SearchResults).Count;
            activity?.SetTag("tailwindtraders.image_search.results", count);
            outcome = count > 0 ? "match" : "no_match";

            return result;
            }
            catch (System.Exception e)
            {
                activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, e.Message);
                throw;
            }
            finally
            {
                var tag = new System.Collections.Generic.KeyValuePair<string, object>("result", outcome);
                AppTelemetry.ImageSearchResults.Add(1, tag);
                AppTelemetry.ImageSearchDuration.Record(System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalSeconds, tag);
            }
        }
    }
}