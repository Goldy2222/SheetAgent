using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using System.Net.Http.Json;
using System.Text.Json;

public class PipelineService
{
    private readonly IConfiguration _config;
    private readonly HttpClient _http;

    public PipelineService(IConfiguration config, IHttpClientFactory factory)
    {
        _config = config;
        _http = factory.CreateClient();
    }

    public async Task RunAsync()
    {
        var sheetsService = BuildSheetsService();
        var spreadsheetId = _config["SpreadsheetId"];
        var sheetName = _config["SheetName"];

        // Step 1: Read rows from Google Sheet
        var rows = await GetRowsAsync(sheetsService, spreadsheetId, sheetName);

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var query = row[0]?.ToString(); // Column A = search query
            if (string.IsNullOrWhiteSpace(query)) continue;

            // Step 2: Tavily web search
            var searchResults = await TavilySearchAsync(query);

            // Step 3: Use search results directly
            var aiAnswer = searchResults.Length > 500 ? searchResults.Substring(0, 500) + "..." : searchResults;

            // Step 4: Write result back to sheet (Column B)
            var rowNumber = i + 2; // 1-indexed + header row
            await UpdateRowAsync(sheetsService, spreadsheetId, sheetName, rowNumber, aiAnswer);
        }
    }

    private SheetsService BuildSheetsService()
    {
        var credPath = _config["GoogleSheetsCredentialPath"];
        GoogleCredential credential;
        using var stream = new FileStream(credPath, FileMode.Open, FileAccess.Read);
        credential = GoogleCredential.FromStream(stream)
            .CreateScoped(SheetsService.Scope.Spreadsheets);

        return new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "SheetAgent"
        });
    }

    private async Task<IList<IList<object>>> GetRowsAsync(
        SheetsService service, string spreadsheetId, string sheetName)
    {
        var range = $"{sheetName}!A2:A"; // Read column A (skip header)
        var request = service.Spreadsheets.Values.Get(spreadsheetId, range);
        var response = await request.ExecuteAsync();
        return response.Values ?? new List<IList<object>>();
    }

    private async Task<string> TavilySearchAsync(string query)
    {
        var apiKey = _config["TavilyApiKey"];
        var payload = new { api_key = apiKey, query, max_results = 3 };

        var response = await _http.PostAsJsonAsync("https://api.tavily.com/search", payload);
        var errorBody = await response.Content.ReadAsStringAsync(); if (!response.IsSuccessStatusCode) throw new Exception($"Gemini error {response.StatusCode}: {errorBody}");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        // Extract top result snippets
        var results = doc.RootElement.GetProperty("results");
        var snippets = new List<string>();
        foreach (var r in results.EnumerateArray())
        {
            if (r.TryGetProperty("content", out var content))
                snippets.Add(content.GetString() ?? "");
        }
        return string.Join("\n\n", snippets);
    }

    private async Task<string> AskGeminiAsync(string query, string context)
    {
        var apiKey = _config["GeminiApiKey"];
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={apiKey}";

        var prompt = $"Based on the following search results, answer this question:\n\nQuestion: {query}\n\nSources:\n{context}";

        var payload = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            }
        };

        var jsonPayload = System.Text.Json.JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(url, content);
        var errorBody = await response.Content.ReadAsStringAsync(); if (!response.IsSuccessStatusCode) throw new Exception($"Gemini error {response.StatusCode}: {errorBody}");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        return doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "No response";
    }

    private async Task UpdateRowAsync(
        SheetsService service, string spreadsheetId, string sheetName, int rowNum, string value)
    {
        var range = $"{sheetName}!B{rowNum}"; // Write to column B
        var body = new ValueRange
        {
            Values = new List<IList<object>> { new List<object> { value } }
        };

        var request = service.Spreadsheets.Values.Update(body, spreadsheetId, range);
        request.ValueInputOption = SpreadsheetsResource.ValuesResource
            .UpdateRequest.ValueInputOptionEnum.RAW;

        await request.ExecuteAsync();
    }
}


