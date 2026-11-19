using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GeoCoinStart;

internal sealed class GeoCoinRpcClient : IDisposable
{
    private readonly HttpClient _http;
    private int _requestId;

    public GeoCoinRpcClient(string username, string password)
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        string token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
    }

    public async Task<JsonElement> CallAsync(string method, object? parameters = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = Interlocked.Increment(ref _requestId),
            ["method"] = method,
            ["params"] = parameters ?? Array.Empty<object>()
        };

        using var body = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync("http://127.0.0.1:3033/", body);
        string json = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        using JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("error", out JsonElement error) &&
            error.ValueKind != JsonValueKind.Null)
        {
            string message = error.TryGetProperty("message", out JsonElement errorMessage)
                ? errorMessage.GetString() ?? "Ошибка GeoCoin"
                : error.ToString();
            throw new InvalidOperationException(message);
        }

        if (!document.RootElement.TryGetProperty("result", out JsonElement result))
            throw new InvalidOperationException("Нода вернула непонятный ответ.");
        return result.Clone();
    }

    public void Dispose() => _http.Dispose();
}
