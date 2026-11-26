using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GeoCoinStart;

internal sealed class GeoCoinRpcClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private int _requestId;

    public GeoCoinRpcClient(string username, string password, string endpoint = "http://127.0.0.1:3033/", bool subminer = false)
    {
        _endpoint = new Uri(endpoint, UriKind.Absolute);
        _http = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };
        string token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        if (subminer) _http.DefaultRequestHeaders.Add("X-GeoCoin-Client", "subminer");
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
        using var response = await _http.PostAsync(_endpoint, body);
        string json = await response.Content.ReadAsStringAsync();

        if ((int)response.StatusCode == 421)
            throw new HttpRequestException(
                "RPC отклонил заголовок Host. Используйте порт 3033 на обеих сторонах SSH-туннеля.",
                null, response.StatusCode);

        JsonDocument parsed;
        try { parsed = JsonDocument.Parse(json); }
        catch (JsonException)
        {
            response.EnsureSuccessStatusCode();
            throw new InvalidOperationException("Нода вернула ответ в формате, отличном от JSON.");
        }
        using JsonDocument document = parsed;
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            response.EnsureSuccessStatusCode();
            throw new InvalidOperationException("Нода вернула непонятный ответ.");
        }
        if (document.RootElement.TryGetProperty("error", out JsonElement error) &&
            error.ValueKind != JsonValueKind.Null)
        {
            string message = error.TryGetProperty("message", out JsonElement errorMessage)
                ? errorMessage.GetString() ?? "Ошибка GeoCoin"
                : error.ToString();
            int? code = error.TryGetProperty("code", out JsonElement errorCode) && errorCode.TryGetInt32(out int parsedCode)
                ? parsedCode
                : null;
            throw new GeoCoinRpcException(code, message);
        }

        response.EnsureSuccessStatusCode();

        if (!document.RootElement.TryGetProperty("result", out JsonElement result))
            throw new InvalidOperationException("Нода вернула непонятный ответ.");
        return result.Clone();
    }

    public void Dispose() => _http.Dispose();
}

internal sealed class GeoCoinRpcException : InvalidOperationException
{
    public GeoCoinRpcException(int? code, string message) : base(message) => Code = code;

    public int? Code { get; }
}
