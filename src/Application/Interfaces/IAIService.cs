namespace Application.Interfaces;

public interface IAIService
{
    Task<string> GenerateSQLFromNaturalLanguageAsync(string userQuery, string tableSchema);
    Task<string> SuggestVisualizationAsync(string query, string resultData);
}