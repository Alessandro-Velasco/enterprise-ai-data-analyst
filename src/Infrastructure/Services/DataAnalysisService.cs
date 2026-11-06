using Microsoft.Extensions.Configuration;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Infrastructure.Services;

public class DataAnalysisService : IDataAnalysisService
{
    private readonly AppDbContext _context;
    private readonly IAIService _aiService;
    private readonly string _uploadPath;

    public DataAnalysisService(AppDbContext context, IAIService aiService, IConfiguration config)
    {
        _context = context;
        _aiService = aiService;
        _uploadPath = config["UploadPath"] ?? "uploads";
        Directory.CreateDirectory(_uploadPath);
    }

    public async Task<Dataset> UploadDatasetAsync(string fileName, Stream fileStream)
    {
        var extension = Path.GetExtension(fileName).ToLower();
        var filePath = Path.Combine(_uploadPath, $"{Guid.NewGuid()}{extension}");
        
        using (var fileStreamOutput = File.Create(filePath))
        {
            await fileStream.CopyToAsync(fileStreamOutput);
        }

        var columns = await ParseCsvSchemaAsync(filePath);
        
        var dataset = new Dataset
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileNameWithoutExtension(fileName),
            FilePath = filePath,
            FileType = extension.TrimStart('.'),
            UploadedAt = DateTime.UtcNow,
            ColumnsJson = JsonSerializer.Serialize(columns) // <-- QUI USA ColumnsJson
        };

        _context.Datasets.Add(dataset);
        await _context.SaveChangesAsync();
        return dataset;
    }

    public async Task<List<Dataset>> GetAllDatasetsAsync()
    {
        return await _context.Datasets.ToListAsync();
    }

    public async Task<Dataset> GetDatasetSchemaAsync(Guid datasetId)
    {
        return await _context.Datasets.FirstOrDefaultAsync(d => d.Id == datasetId)
            ?? throw new Exception("Dataset non trovato");
    }

    public async Task<QueryResult> ExecuteNaturalQueryAsync(Guid datasetId, string userQuery)
    {
        var dataset = await GetDatasetSchemaAsync(datasetId);
        
        // <-- QUI DESERIALIZZIAMO ColumnsJson
        var columns = JsonSerializer.Deserialize<List<DataColumn>>(dataset.ColumnsJson) ?? new List<DataColumn>();
        
        var schema = $"Tabella: {dataset.Name}\nColonne:\n" + 
                     string.Join("\n", columns.Select(c => $"- {c.Name} ({c.DataType})"));
        
        var generatedSQL = await _aiService.GenerateSQLFromNaturalLanguageAsync(userQuery, schema);
        var resultData = "[{\"message\": \"Dati simulati per MVP\"}]"; 
        var vizType = await _aiService.SuggestVisualizationAsync(userQuery, resultData);

        var queryResult = new QueryResult
        {
            Id = Guid.NewGuid(),
            DatasetId = datasetId,
            UserQuery = userQuery,
            GeneratedSQL = generatedSQL,
            ResultJSON = resultData,
            VisualizationType = vizType,
            ExecutedAt = DateTime.UtcNow
        };

        _context.QueryResults.Add(queryResult);
        await _context.SaveChangesAsync();
        return queryResult;
    }

    private Task<List<DataColumn>> ParseCsvSchemaAsync(string filePath)
    {
        var columns = new List<DataColumn>();
        if (File.Exists(filePath))
        {
            var firstLine = File.ReadLines(filePath).FirstOrDefault();
            if (firstLine != null)
            {
                foreach (var col in firstLine.Split(','))
                {
                    columns.Add(new DataColumn { Name = col.Trim(), DataType = "string", IsNullable = true });
                }
            }
        }
        return Task.FromResult(columns);
    }
}