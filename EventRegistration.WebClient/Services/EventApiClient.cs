using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace EventRegistration.WebClient.Services;

public class EventApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EventApiClient(HttpClient http, IHttpContextAccessor httpContextAccessor)
    {
        _http = http;
        _httpContextAccessor = httpContextAccessor;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        var token = _httpContextAccessor.HttpContext?.Session.GetString("JwtToken");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
            request.Content = JsonContent.Create(body, options: JsonOptions);

        return request;
    }

    public async Task<T?> GetAsync<T>(string url)
    {
        using var request = CreateRequest(HttpMethod.Get, url);
        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return default;
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    public async Task<(bool Success, T? Data, string? Error, int StatusCode)> PostAsync<T>(string url, object body)
    {
        using var request = CreateRequest(HttpMethod.Post, url, body);
        var response = await _http.SendAsync(request);
        return await ParseResponse<T>(response);
    }

    public async Task<(bool Success, T? Data, string? Error, int StatusCode)> PutAsync<T>(string url, object body)
    {
        using var request = CreateRequest(HttpMethod.Put, url, body);
        var response = await _http.SendAsync(request);
        return await ParseResponse<T>(response);
    }

    public async Task<(bool Success, string? Error, int StatusCode)> DeleteAsync(string url)
    {
        using var request = CreateRequest(HttpMethod.Delete, url);
        var response = await _http.SendAsync(request);
        if (response.IsSuccessStatusCode)
            return (true, null, (int)response.StatusCode);

        var error = await ReadErrorAsync(response);
        return (false, error, (int)response.StatusCode);
    }

    private static async Task<(bool Success, T? Data, string? Error, int StatusCode)> ParseResponse<T>(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
            return (true, data, null, (int)response.StatusCode);
        }

        var error = await ReadErrorAsync(response);
        return (false, default, error, (int)response.StatusCode);
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json))
                return response.ReasonPhrase;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("error", out var errorProp) && errorProp.ValueKind == JsonValueKind.String)
                    return errorProp.GetString();

                if (root.TryGetProperty("errors", out var errorsProp) && errorsProp.ValueKind == JsonValueKind.Object)
                {
                    var messages = new List<string>();
                    foreach (var prop in errorsProp.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in prop.Value.EnumerateArray())
                            {
                                if (item.ValueKind == JsonValueKind.String)
                                    messages.Add(item.GetString()!);
                            }
                        }
                        else if (prop.Value.ValueKind == JsonValueKind.String)
                        {
                            messages.Add(prop.Value.GetString()!);
                        }
                    }
                    if (messages.Count > 0)
                        return string.Join(" ", messages);
                }

                if (root.TryGetProperty("detail", out var detailProp) && detailProp.ValueKind == JsonValueKind.String)
                    return detailProp.GetString();

                if (root.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String)
                    return titleProp.GetString();
            }

            return json;
        }
        catch
        {
            return response.ReasonPhrase;
        }
    }
}
