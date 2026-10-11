using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Tailwind.Traders.Web.Standalone.Ai;

namespace Tailwind.Traders.Web.Standalone.Controllers
{
    [ApiController]
    public class AiController : ControllerBase
    {
        private readonly ISemanticSearchService search;
        public AiController(ISemanticSearchService search) => this.search = search;

        /// <summary>Semantic product search (pgvector cosine similarity over product embeddings).</summary>
        [HttpGet("api/v1/products/search")]
        public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int take = 12, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(q)) return BadRequest(new { message = "q is required" });
            if (q.Length > 500) return BadRequest(new { message = "q is too long (max 500)" });
            return Ok(await search.SearchAsync(q.Trim(), System.Math.Clamp(take, 1, 50), ct));
        }

        public class AssistantRequest { public string Question { get; set; } }

        /// <summary>Optional RAG assistant; 404 unless Ai:Assistant:Enabled=true.</summary>
        [HttpPost("api/v1/assistant")]
        public async Task<IActionResult> Ask([FromBody] AssistantRequest req, [FromServices] IOptions<AiOptions> options,
            [FromServices] ShoppingAssistant assistant, CancellationToken ct)
        {
            if (!options.Value.Assistant.Enabled) return NotFound();
            if (string.IsNullOrWhiteSpace(req?.Question) || req.Question.Length > 500) return BadRequest(new { message = "question is required (max 500 chars)" });
            return Ok(await assistant.AskAsync(req.Question.Trim(), ct));
        }
    }
}
