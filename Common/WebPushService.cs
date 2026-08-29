using apiprojnew.Data;
using Microsoft.EntityFrameworkCore;
using WebPush;

namespace apiprojnew.Common
{
    /// <summary>Outcome of one delivery attempt, so failures stop being invisible.</summary>
    public class PushSendResult
    {
        public int Attempted { get; set; }
        public int Delivered { get; set; }
        public int Removed { get; set; }
        public List<string> Failures { get; set; } = new();
    }

    public class WebPushService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly WebPushClient _client;
        private readonly VapidDetails _vapid;
        private readonly string _publicKey;

        public WebPushService(IServiceScopeFactory scopeFactory, IConfiguration config)
        {
            _scopeFactory = scopeFactory;
            _publicKey = config["WebPush:PublicKey"] ?? "";
            var subject = config["WebPush:Subject"] ?? "";
            var privateKey = config["WebPush:PrivateKey"] ?? "";

            IsConfigured = !string.IsNullOrWhiteSpace(_publicKey)
                        && !string.IsNullOrWhiteSpace(privateKey)
                        && (subject.StartsWith("mailto:") || subject.StartsWith("https://"));

            if (!IsConfigured)
            {
                // Push services reject a VAPID subject that is not a mailto:/https: URI,
                // so say so loudly at boot rather than failing silently on every send.
                Console.WriteLine("[WebPush] NOT CONFIGURED — check WebPush:Subject (must be mailto: or https:), PublicKey and PrivateKey.");
            }

            _vapid = new VapidDetails(subject, _publicKey, privateKey);
            _client = new WebPushClient();
        }

        /// <summary>False when VAPID settings are missing or malformed.</summary>
        public bool IsConfigured { get; }

        public string GetPublicKey() => _publicKey;

        // Send to one specific user (by userId)
        public async Task<PushSendResult> SendToUserAsync(int userId, string title, string body, string url = "/dashboard")
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();

            var subs = await db.PushSubscriptions
                .Where(s => s.UserId == userId)
                .ToListAsync();

            Console.WriteLine($"[WebPush] Sending to {subs.Count} subscription(s) for user {userId} (Title: '{title}')...");
            return await SendBatchAsync(subs, title, body, url);
        }

        // Send to ALL subscribed users
        public async Task<PushSendResult> SendToAllAsync(string title, string body, string url = "/")
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();

            var subs = await db.PushSubscriptions.ToListAsync();
            Console.WriteLine($"[WebPush] Broadcasting to {subs.Count} subscription(s) (Title: '{title}')...");
            return await SendBatchAsync(subs, title, body, url);
        }

        // Send to all users on a specific phase
        public async Task<PushSendResult> SendToPhaseAsync(int phase, string title, string body, string url = "/dashboard")
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();

            var subs = await db.PushSubscriptions
                .Include(s => s.User)
                .Where(s => s.User != null && (int)s.User.UserPahse == phase)
                .ToListAsync();

            return await SendBatchAsync(subs, title, body, url);
        }

        private async Task<PushSendResult> SendBatchAsync(
            List<Models.PushSubscriptionModel> subscriptions,
            string title, string body, string url)
        {
            var result = new PushSendResult { Attempted = subscriptions.Count };

            if (!IsConfigured)
            {
                result.Failures.Add("WebPush is not configured on the server (VAPID subject/keys).");
                Console.WriteLine("[WebPush] Send aborted — service is not configured.");
                return result;
            }

            // A stable tag per destination means a newer update about the same
            // thing replaces the older one instead of stacking on the lock screen.
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                title,
                body,
                url,
                tag = "geto-" + url,
                icon = "/icons/icon-192.png",
                badge = "/icons/icon-192.png"
            });

            var staleIds = new List<int>();
            var pushOptions = new Dictionary<string, object>
            {
                { "vapidDetails", _vapid },
                { "TTL", 86400 },
                { "headers", new Dictionary<string, object>
                    {
                        // "high" keeps the message out of the push service's
                        // batching queue, so a locked phone is woken right away.
                        { "Urgency", "high" }
                    }
                }
            };

            foreach (var sub in subscriptions)
            {
                try
                {
                    var pushSub = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                    await _client.SendNotificationAsync(pushSub, payload, pushOptions);
                    result.Delivered++;
                }
                catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone
                                                || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // The browser dropped this subscription — clean it up so the
                    // user is prompted to re-subscribe rather than silently muted.
                    staleIds.Add(sub.Id);
                    Console.WriteLine($"[WebPush] Sub {sub.Id} is gone ({(int)ex.StatusCode}); removing.");
                }
                catch (WebPushException ex)
                {
                    // The status code is the one thing that explains a failed
                    // delivery (401/403 = bad VAPID, 413 = payload too large,
                    // 429 = rate limited). The old code threw it away.
                    var detail = $"{(int)ex.StatusCode} {ex.StatusCode} — {ex.Message}";
                    result.Failures.Add(detail);
                    Console.WriteLine($"[WebPush] Sub {sub.Id} ({Provider(sub.Endpoint)}) failed: {detail}");
                }
                catch (Exception ex)
                {
                    result.Failures.Add(ex.Message);
                    Console.WriteLine($"[WebPush] Sub {sub.Id} failed: {ex.Message}");
                }
            }

            if (staleIds.Count > 0)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DataContext>();
                var stale = db.PushSubscriptions.Where(s => staleIds.Contains(s.Id));
                db.PushSubscriptions.RemoveRange(stale);
                await db.SaveChangesAsync();
                result.Removed = staleIds.Count;
            }

            Console.WriteLine($"[WebPush] Done: {result.Delivered}/{result.Attempted} delivered, {result.Removed} removed, {result.Failures.Count} failed.");
            return result;
        }

        /// <summary>Which push service an endpoint belongs to — useful in logs.</summary>
        public static string Provider(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint)) return "unknown";
            if (endpoint.Contains("fcm.googleapis.com") || endpoint.Contains("android.googleapis.com")) return "Chrome/Android";
            if (endpoint.Contains("mozilla.com") || endpoint.Contains("mozaws.net")) return "Firefox";
            if (endpoint.Contains("windows.com")) return "Edge";
            if (endpoint.Contains("push.apple.com")) return "Safari/iOS";
            return "other";
        }
    }
}
