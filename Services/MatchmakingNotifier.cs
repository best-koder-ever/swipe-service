using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SwipeService.Services
{
    public class MatchmakingNotifier
    {
        private readonly HttpClient _httpClient;

        public MatchmakingNotifier(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public virtual async Task NotifyMatchmakingServiceAsync(int userId, int targetUserId)
        {
            var matchData = new { User1Id = userId, User2Id = targetUserId };
            var content = new StringContent(JsonSerializer.Serialize(matchData), Encoding.UTF8, "application/json");

            var baseUrl = Environment.GetEnvironmentVariable("MATCHMAKING_SERVICE_URL") ?? "http://localhost:8083";
            var requestUri = $"{baseUrl.TrimEnd('/')}/api/matchmaking/matches";

            var response = await _httpClient.PostAsync(requestUri, content);
            if (!response.IsSuccessStatusCode)
            {
                // Log or handle the error
                throw new HttpRequestException($"Failed to notify MatchmakingService: {response.StatusCode}");
            }
        }
    }
}