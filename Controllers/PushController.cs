using apiprojnew.Common;
using apiprojnew.Data;
using apiprojnew.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace apiprojnew.Controllers
{
    [Route("getoProject/[controller]")]
    [ApiController]
    public class PushController : ControllerBase
    {
        private readonly DataContext _db;
        private readonly WebPushService _pushService;

        public PushController(DataContext db, WebPushService pushService)
        {
            _db = db;
            _pushService = pushService;
        }

        // Public — frontend needs this before login to subscribe
        [HttpGet("vapid-public-key")]
        [AllowAnonymous]
        public IActionResult GetVapidPublicKey()
        {
            return Ok(new { publicKey = _pushService.GetPublicKey() });
        }

        // Save browser push subscription for the logged-in user
        [HttpPost("subscribe")]
        [Authorize]
        public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest req)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(req.Endpoint) ||
                string.IsNullOrWhiteSpace(req.P256dh) ||
                string.IsNullOrWhiteSpace(req.Auth))
                return BadRequest("Invalid subscription data");

            // Upsert: remove existing subscription for this endpoint, then add new
            var existing = await _db.PushSubscriptions
                .Where(s => s.UserId == userId && s.Endpoint == req.Endpoint)
                .ToListAsync();

            _db.PushSubscriptions.RemoveRange(existing);

            _db.PushSubscriptions.Add(new PushSubscriptionModel
            {
                UserId = userId,
                Endpoint = req.Endpoint,
                P256dh = req.P256dh,
                Auth = req.Auth,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            return Ok(new { message = "Subscribed successfully" });
        }

        // Remove subscription (user unsubscribed in browser)
        [HttpDelete("unsubscribe")]
        [Authorize]
        public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequest req)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
                return Unauthorized();

            var subs = await _db.PushSubscriptions
                .Where(s => s.UserId == userId && s.Endpoint == req.Endpoint)
                .ToListAsync();

            _db.PushSubscriptions.RemoveRange(subs);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Unsubscribed" });
        }
    }

    public class SubscribeRequest
    {
        public string Endpoint { get; set; } = string.Empty;
        public string P256dh { get; set; } = string.Empty;
        public string Auth { get; set; } = string.Empty;
    }

    public class UnsubscribeRequest
    {
        public string Endpoint { get; set; } = string.Empty;
    }
}
