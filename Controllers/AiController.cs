using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace apiprojnew.Controllers
{
    [Route("getoProject/[controller]")]
    [ApiController]
    public class AiController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;

        public AiController(IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("models")]
        public async Task<IActionResult> GetModels()
        {
            var apiKey = GetApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
                return BadRequest(new { error = "No API key configured" });

            bool isGroq = apiKey.StartsWith("gsk_", StringComparison.OrdinalIgnoreCase);
            var endpoint = isGroq
                ? "https://api.groq.com/openai/v1/models"
                : "https://api.openai.com/v1/models";

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            try
            {
                var response = await client.GetAsync(endpoint);
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] ChatRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request?.Message))
                return BadRequest(new { error = "Message is required" });

            var apiKey = GetApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
                return StatusCode(500, new { error = "AI service not configured: API key missing." });

            bool isGroq = apiKey.StartsWith("gsk_", StringComparison.OrdinalIgnoreCase);
            var endpoint = isGroq
                ? "https://api.groq.com/openai/v1/chat/completions"
                : "https://api.openai.com/v1/chat/completions";
            var customModel = _config["Ai:Model"] ?? Environment.GetEnvironmentVariable("AI_MODEL");

            var systemPrompt = @"You are a helpful assistant for GETO Project — a Georgian student employment program in Germany.
You help Georgian students with questions about:
- Registration process: uploading CV, passport scan, student status certificate, Immatrikulation form, payment receipt
- Documents required: CV (resume), passport scan (PDF), student status certificate, Immatrikulation, registration fee payment receipt
- Registration is officially confirmed only after the registration fee is paid AND the CV is submitted
- All documents must be uploaded in PDF format. Passport scanning is available at office service centres
- Vacancies, wages, accommodation, and working conditions in Germany are available in the News section of the website
- Users can upload documents (PDF, DOCX) in their personal dashboard
- Admin panel manages users and sends documents
- If asked about something unrelated to GETO Project, politely redirect to relevant topics

Always respond concisely and helpfully. If the user writes in Georgian, respond in Georgian. If in English, respond in English.";

            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt }
            };

            // Add chat history if provided
            if (request.History != null)
            {
                foreach (var h in request.History)
                {
                    messages.Add(new { role = h.Role, content = h.Content });
                }
            }

            messages.Add(new { role = "user", content = request.Message });

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var candidateModels = new List<string>();
            if (!string.IsNullOrWhiteSpace(customModel)) candidateModels.Add(customModel);

            if (isGroq)
            {
                candidateModels.AddRange(new[] {
                    "llama-3.3-70b-versatile",
                    "llama-3.1-8b-instant",
                    "gemma2-9b-it",
                    "mixtral-8x7b-32768",
                    "llama3-8b-8192",
                    "llama3-70b-8192"
                });
            }
            else
            {
                candidateModels.Add("gpt-4o-mini");
            }

            string? lastError = null;

            foreach (var model in candidateModels.Distinct())
            {
                var payload = new
                {
                    model,
                    messages,
                    max_tokens = 500,
                    temperature = 0.7
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                try
                {
                    var response = await client.PostAsync(endpoint, content);
                    var responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        using var doc = JsonDocument.Parse(responseBody);
                        var reply = doc.RootElement
                            .GetProperty("choices")[0]
                            .GetProperty("message")
                            .GetProperty("content")
                            .GetString();

                        return Ok(new { reply });
                    }

                    lastError = responseBody;

                    // If it's not a model availability error, don't keep trying other models
                    if (!responseBody.Contains("model_not_found"))
                    {
                        return StatusCode(500, new { error = "AI service error", details = responseBody });
                    }
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { error = "Request failed", details = ex.Message });
                }
            }

            return StatusCode(500, new { error = "AI service error", details = lastError });
        }

        private string? GetApiKey()
        {
            var apiKey = _config["Groq:ApiKey"]
                ?? _config["Groq__ApiKey"]
                ?? _config["GROQ_API_KEY"]
                ?? _config["OpenAI:ApiKey"]
                ?? _config["OpenAI__ApiKey"]
                ?? _config["OPENAI_API_KEY"]
                ?? Environment.GetEnvironmentVariable("Groq__ApiKey")
                ?? Environment.GetEnvironmentVariable("GROQ_API_KEY")
                ?? Environment.GetEnvironmentVariable("OpenAI__ApiKey")
                ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

            if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_OPENAI_API_KEY_HERE")
                return null;

            return apiKey.Trim();
        }
    }

    public class ChatRequestDto
    {
        public string Message { get; set; } = "";
        public List<ChatHistoryItem>? History { get; set; }
    }

    public class ChatHistoryItem
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = "";
    }
}
