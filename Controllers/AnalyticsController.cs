using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using apiprojnew.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace apiprojnew.Controllers;

[ApiController]
[Authorize]
[Route("getoProject/admin/analytics")]
public class AnalyticsController : ControllerBase
{
    private const string DefaultProjectId = "prj_Ve7XqKJBXxY4p82hAbl5W3bEt4Af";
    private const string DefaultTeamId = "team_nQ9SCPLp9DzhPKlyaPFOYzy4";
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(IConfiguration configuration, IHttpClientFactory httpClientFactory,
        ILogger<AnalyticsController> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (User.FindFirst(ClaimTypes.Role)?.Value != UserRoles.Admin.ToString())
            return Forbid();

        var token = _configuration["VERCEL_ANALYTICS_TOKEN"];
        if (string.IsNullOrWhiteSpace(token))
            return Ok(new { configured = false });

        var projectId = _configuration["VERCEL_ANALYTICS_PROJECT_ID"] ?? DefaultProjectId;
        var teamId = _configuration["VERCEL_ANALYTICS_TEAM_ID"] ?? DefaultTeamId;
        var commonQuery = $"projectId={Uri.EscapeDataString(projectId)}&teamId={Uri.EscapeDataString(teamId)}";
        var since = DateTime.UtcNow.Date.AddDays(-14).ToString("yyyy-MM-dd");
        var until = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
        var baseUrl = "https://api.vercel.com/v1/query/web-analytics/visits/";

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            using var count = await QueryAsync(client, token, $"{baseUrl}count?{commonQuery}&since={since}&until={until}", cancellationToken);
            using var daily = await QueryAsync(client, token,
                $"{baseUrl}aggregate?{commonQuery}&by=day&since={since}&until={until}&limit=15", cancellationToken);
            using var pages = await QueryAsync(client, token,
                $"{baseUrl}aggregate?{commonQuery}&by=requestPath&since={since}&until={until}&limit=6", cancellationToken);

            return Ok(new
            {
                configured = true,
                pageviews = count.RootElement.GetProperty("data").GetProperty("pageviews").GetInt64(),
                visitors = count.RootElement.GetProperty("data").GetProperty("visitors").GetInt64(),
                daily = daily.RootElement.GetProperty("data").Clone(),
                topPages = pages.RootElement.GetProperty("data").Clone()
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Vercel Analytics request failed");
            return StatusCode(502, new { message = "Vercel Analytics could not be loaded. Check the token, project access and whether Web Analytics is enabled." });
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Vercel Analytics response could not be read");
            return StatusCode(502, new { message = "Vercel Analytics returned an unexpected response. Please retry later." });
        }
    }

    private static async Task<JsonDocument> QueryAsync(HttpClient client, string token, string url,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }
}
