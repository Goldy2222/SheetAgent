# SheetAgent 🤖

A C# Worker Service that automatically generates LinkedIn posts using AI.

## How it works
1. Reads topics from a Google Sheet (Column A)
2. Searches the web using Tavily API
3. Generates LinkedIn posts using Google Gemini AI
4. Writes the posts back to the Google Sheet (Column C)

## Setup
1. Clone the repo
2. Add your `credentials.json` from Google Cloud
3. Update `appsettings.json` with your API keys:
   - Google Sheets Spreadsheet ID
   - Tavily API Key
   - Gemini API Key
4. Run with `dotnet run`

## Requirements
- .NET 10
- Google Cloud Service Account
- Tavily API Key
- Google Gemini API Key
