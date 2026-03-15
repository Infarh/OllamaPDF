using System.Text;
using System.Text.Json;

using Ollama;

using UglyToad.PdfPig;

const string model_name = "mistral:latest";

const string data_path = @"d:\123\pdf";

var data_dir = new DirectoryInfo(data_path);
if (!data_dir.Exists)
{
    Console.WriteLine("Директория не существует");
    return -1;
}

const int max_text_len = 5000;     // Максимальный размер текста, читаемый из файла
const int max_attempts = 3;        // число попыток обращения к модели
const int request_timeout_sec = 30; // таймаут одного запроса к модели в секундах

var file_text = new StringBuilder(max_text_len * 2);


foreach (var data_file in data_dir.EnumerateFiles("*.pdf", SearchOption.AllDirectories))
{
    Console.WriteLine($"Обработка файла {data_file.RelativePath(data_dir)}");
    Console.WriteLine($"                   размер: {data_file.Length.ToDataLen(out var unit),6:N2} {unit}");
    var pdf = PdfDocument.Open(data_file.FullName, new ParsingOptions { UseLenientParsing = true });
    Console.WriteLine($"            число страниц: {pdf.NumberOfPages}");

    file_text.Clear();
    foreach (var page in pdf.GetPages())
    {
        var page_text = page.Text;
        if (page_text.Length <= 0) continue;
        file_text.Append(page_text);

        if (file_text.Length >= max_text_len)
        {
            file_text.Length = max_text_len;
            var cut = file_text.ToString().LastIndexOfAny(['.', '!', '?']); // граница предложения
            if (cut > max_text_len / 2)
                file_text.Length = cut + 1;
            break;
        }
    }

    using var ollama = new OllamaApiClient();

    Console.WriteLine("   Запрос имени и описания у модели...");

    var api_request = new GenerateCompletionRequest
    {
        Model = model_name,
        Format = ResponseFormatEnum.Json,
        Prompt = $$"""
        Analyze the PDF book text below and provide a filename and a summary.

        Filename requirements:
        - Use only Latin letters, digits, and underscores.
        - No spaces or special characters.
        - No file extension.
        - Transliterate non-Latin titles to Latin.
        - Avoid generic words like "book", "file", "pdf".
        - Maximum 100 characters.

        Summary requirements:
        - Concise and informative, maximum 500 characters.
        - Avoid clichés. Focus on unique aspects of the book content.
        - No template phrases like "This book is about...". Provide specific details.
        - Write the summary in Russian.

        Respond ONLY with a valid JSON object in this exact format:
        {"filename": "suggested_filename", "summary": "краткое описание на русском"}

        Book text:
        {{file_text}}
        """,
        Stream = false,
    };

    string? model_result = null;
    for (var attempt = 1; attempt <= max_attempts; attempt++)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(request_timeout_sec));
            var r = await ollama.Completions.GenerateCompletionAsync(api_request, cts.Token);
            model_result = r.Response;
            break;
        }
        catch (OperationCanceledException)
        {
            if (attempt < max_attempts)
                Console.WriteLine($"   [{attempt}/{max_attempts}] Таймаут запроса. Повтор...");
        }
    }

    if (model_result is null)
    {
        Console.WriteLine($"   Не удалось получить ответ от модели за {max_attempts} попытки(-ок). Файл пропущен.");
        Console.WriteLine();
        continue;
    }

    var result = model_result;

    string suggested_name;
    string summary;
    try
    {
        var json = JsonSerializer.Deserialize<JsonElement>(result);
        suggested_name = json.GetProperty("filename").GetString()?.Trim() ?? string.Empty;
        summary = json.GetProperty("summary").GetString()?.Trim() ?? string.Empty;
    }
    catch (JsonException)
    {
        suggested_name = result.Trim();
        summary = string.Empty;
        Console.WriteLine($"Ошибка при разборе JSON-ответа модели. Текст ответа: {result}");
    }

    Console.WriteLine($"    предложенное название: {suggested_name}{data_file.Extension}");
    Console.WriteLine("         краткое описание:");
    Console.WriteLine(summary.IdentLines("        "));

    Console.WriteLine();
}

return 0;

file static class Ex
{
    extension(FileInfo file)
    {
        public string RelativePath(DirectoryInfo BaseDir)
        {
            var base_path = BaseDir.FullName;
            var file_path = file.FullName;
            return file_path.StartsWith(base_path, StringComparison.OrdinalIgnoreCase)
                ? file_path[base_path.Length..].TrimStart(Path.DirectorySeparatorChar)
                : throw new ArgumentException("File is not in the base directory");
        }
    }

    extension(long n)
    {
        public double ToDataLen(out string unit)
        {
            if (n >= 1L << 30)
            {
                unit = "ГБ";
                return n / (double)(1L << 30);
            }
            else if (n >= 1L << 20)
            {
                unit = "МБ";
                return n / (double)(1L << 20);
            }
            else if (n >= 1L << 10)
            {
                unit = "кБ";
                return n / (double)(1L << 10);
            }
            else
            {
                unit = "Б";
                return n;
            }
        }
    }

    extension(string str)
    {
        public string IdentLines(string IdentStr)
        {
            var result = new StringBuilder();
            foreach (var line in str.EnumLines())
                if (line.Length > 0)
                    result.Append(IdentStr).AppendLine(line);
                else
                    result.AppendLine();

            return result.ToString();
        }

        public IEnumerable<string> EnumLines()
        {
            using var reader = new StringReader(str);
            string? line;
            while ((line = reader.ReadLine()) != null)
                yield return line;
        }
    }
}