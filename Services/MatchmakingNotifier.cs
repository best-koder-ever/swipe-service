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

        public async Task NotifyMatchmakingServiceAsync(int userId, int targetUserId)
        {
            var matchData = new { User1Id = userId, User2Id = targetUserId };
            var content = new StringContent(JsonSerializer.Serialize(matchData), Encoding.UTF8, "application/json");

            // Fixed endpoint path to match MatchmakingController route
            var response = await _httpClient.PostAsync("http://MatchmakingService:8083/api/matchmaking/matches", content);
            if (!response.IsSuccessStatusCode)
            {
                // Log or handle the error
                throw new HttpRequestException($"Failed to notify MatchmakingService: {response.StatusCode}");
            }
        }
    }
}