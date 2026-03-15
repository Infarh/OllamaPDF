using Ollama;

const string model_name = "mistral:latest";

using var ollama = new OllamaApiClient();

var response = await ollama.Completions.GenerateCompletionAsync(new()
{
    Model = model_name,
    Prompt = "Напиши стихотворение про лето",
    Stream = false,
});

var result = response.Response;
Console.WriteLine(result);


return;

