

using System.Net.Http.Json;

var ollama = OllamaClientFactory.CreateLocalQwenClient();

Console.WriteLine($"Ollama client initialized for model: {ollama.Model}");

return;

public sealed record OllamaSettings(Uri BaseAddress, string Model);

public static class OllamaClientFactory
{
    public static OllamaClient CreateLocalQwenClient(HttpClient? http = null)
    {
        var settings = new OllamaSettings(
            BaseAddress: new Uri("http://localhost:11434"),
            Model: "qwen3.5:latest");

        return OllamaClient.Create(settings, http);
    }
}

public sealed class OllamaClient
{
    private readonly HttpClient httpClient;

    private OllamaClient(HttpClient httpClient, string model)
    {
        this.httpClient = httpClient;
        Model = model;
    }

    public string Model { get; }

    public static OllamaClient Create(OllamaSettings settings, HttpClient? httpClient = null)
    {
        var client = httpClient ?? new HttpClient();

        if (client.BaseAddress is null)
        {
            client.BaseAddress = settings.BaseAddress;
        }

        return new OllamaClient(client, settings.Model);
    }

    public async Task<string> GenerateAsync(string Prompt, CancellationToken Cancel = default)
    {
        var request = new OllamaGenerateRequest(Model, Prompt, Stream: false);

        using var response = await httpClient.PostAsJsonAsync("/api/generate", request, Cancel);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: Cancel);
        if (payload is null || string.IsNullOrWhiteSpace(payload.Response))
            throw new InvalidOperationException("Ollama returned an empty response.");

        return payload.Response;
    }

    private sealed record OllamaGenerateRequest(string Model, string Prompt, bool Stream);

    private sealed record OllamaGenerateResponse(string Response);
}
