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

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] ChatRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request?.Message))
                return BadRequest(new { error = "Message is required" });

            var apiKey = _config["OpenAI:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_OPENAI_API_KEY_HERE")
            {
                apiKey = _config["OpenAI__ApiKey"]
                    ?? _config["OPENAI_API_KEY"]
                    ?? Environment.GetEnvironmentVariable("OpenAI__ApiKey")
                    ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            }

            if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_OPENAI_API_KEY_HERE")
                return StatusCode(500, new { error = "AI service not configured: OpenAI API key missing. Add OpenAI__ApiKey to Render environment variables." });

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

            var payload = new
            {
                model = "gpt-4o-mini",
                messages,
                max_tokens = 500,
                temperature = 0.7
            };

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return StatusCode(500, new { error = "AI service error", details = responseBody });

                using var doc = JsonDocument.Parse(responseBody);
                var reply = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                return Ok(new { reply });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Request failed", details = ex.Message });
            }
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
