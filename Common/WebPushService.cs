using apiprojnew.Data;
using Microsoft.EntityFrameworkCore;
using WebPush;

namespace apiprojnew.Common
{
    public class WebPushService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly WebPushClient _client;
        private readonly VapidDetails _vapid;
        private readonly string _publicKey;

        public WebPushService(IServiceScopeFactory scopeFactory, IConfiguration config)
        {
            _scopeFactory = scopeFactory;
            _publicKey = config["WebPush:PublicKey"]!;
            _vapid = new VapidDetails(
                subject: config["WebPush:Subject"]!,
                publicKey: _publicKey,
                privateKey: config["WebPush:PrivateKey"]!
            );
            _client = new WebPushClient();
        }

        public string GetPublicKey() => _publicKey;

        // Send to one specific user (by userId)
        public async Task SendToUserAsync(int userId, string title, string body, string url = "/dashboard")
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();

            var subs = await db.PushSubscriptions
                .Where(s => s.UserId == userId)
                .ToListAsync();

            Console.WriteLine($"[WebPush] Sending push notification to {subs.Count} subscription(s) for user ID {userId} (Title: '{title}')...");
            await SendBatchAsync(subs, title, body, url);
        }

        // Send to ALL subscribed users
        public async Task SendToAllAsync(string title, string body, string url = "/")
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();

            var subs = await db.PushSubscriptions.ToListAsync();
            Console.WriteLine($"[WebPush] Sending broadcast notification to {subs.Count} subscription(s) for title: '{title}'...");
            await SendBatchAsync(subs, title, body, url);
        }

        // Send to all users on a specific phase
        public async Task SendToPhaseAsync(int phase, string title, string body, string url = "/dashboard")
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();

            var subs = await db.PushSubscriptions
                .Include(s => s.User)
                .Where(s => s.User != null && (int)s.User.UserPahse == phase)
                .ToListAsync();

            await SendBatchAsync(subs, title, body, url);
        }

        private async Task SendBatchAsync(
            List<Models.PushSubscriptionModel> subscriptions,
            string title, string body, string url)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                title,
                body,
                url,
                icon = "/recommendations/Geto Logo.jpg",
                badge = "/recommendations/Geto Logo.jpg"
            });

            var staleIds = new List<int>();

            foreach (var sub in subscriptions)
            {
                try
                {
                    var pushSub = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                    await _client.SendNotificationAsync(pushSub, payload, _vapid);
                }
                catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone
                                                || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Subscription expired — mark for cleanup
                    staleIds.Add(sub.Id);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WebPush] Failed to send to sub {sub.Id}: {ex.Message}");
                }
            }

            // Clean up stale subscriptions
            if (staleIds.Count > 0)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DataContext>();
                var stale = db.PushSubscriptions.Where(s => staleIds.Contains(s.Id));
                db.PushSubscriptions.RemoveRange(stale);
                await db.SaveChangesAsync();
            }
        }
    }
}
