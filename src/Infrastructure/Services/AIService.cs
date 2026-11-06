using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Interfaces;

namespace Infrastructure.Services;

public class AIService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly string _modelName;

    public AIService(IConfiguration config)
    {
        _httpClient = new HttpClient();
        _modelName = config["Ollama:Model"] ?? "phi3:mini";
    }

    public async Task<string> GenerateSQLFromNaturalLanguageAsync(string userQuery, string tableSchema)
    {
        var prompt = $@"Sei un esperto SQL. Dato questo schema, genera una query SQL per rispondere alla domanda.
Schema: {tableSchema}
Domanda: {userQuery}
Restituisci SOLO la query SQL, senza spiegazioni.
SQL:";

        var payload = new { model = _modelName, prompt = prompt, stream = false };
        
        try 
        {
            // Chiamata diretta all'API locale di Ollama
            var response = await _httpClient.PostAsJsonAsync("http://localhost:11434/api/generate", payload);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            return result.GetProperty("response").GetString()?.Trim() ?? "SELECT * FROM Datasets;";
        }
        catch (Exception ex)
        {
            return $"-- Errore AI (Ollama non attivo?): {ex.Message}";
        }
    }

    public async Task<string> SuggestVisualizationAsync(string query, string resultData)
    {
        await Task.Delay(200); // Simula un piccolo tempo di pensiero
        
        if (query.ToLower().Contains("totale") || query.ToLower().Contains("somma")) 
            return "bar";
        if (query.ToLower().Contains("andamento") || query.ToLower().Contains("tempo")) 
            return "line";
            
        return "table";
    }
}