using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LikeLionChat.Client;

public class QueueClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _serializerOptions;

    public QueueClient(string baseUrl)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };

        _serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<QueueEntryResponse> EnterQueueAsync(string nickname)
    {
        var request = new QueueEntryRequest { Nickname = nickname };
        var response = await _httpClient.PostAsJsonAsync("/api/queue/entry", request, _serializerOptions);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<QueueEntryResponse>(_serializerOptions);
        return result ?? throw new InvalidOperationException("Failed to parse queue entry response");
    }

    public async Task<QueueStatusResponse> GetStatusAsync(string userId)
    {
        var response = await _httpClient.GetAsync($"/api/queue/status?userId={Uri.EscapeDataString(userId)}");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<QueueStatusResponse>(_serializerOptions);
        return result ?? throw new InvalidOperationException("Failed to parse queue status response");
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

public class QueueEntryRequest
{
    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = string.Empty;
}

public class QueueEntryResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("rank")]
    public int Rank { get; set; }

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;
}

public class QueueStatusResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("rank")]
    public int Rank { get; set; }

    [JsonPropertyName("ticketId")]
    public string? TicketId { get; set; }
}
