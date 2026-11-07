using Microsoft.Extensions.Configuration;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using System.Text.RegularExpressions;

namespace Infrastructure.Services;

public class DataAnalysisService : IDataAnalysisService
{
    private readonly AppDbContext _context;
    private readonly IAIService _aiService;
    private readonly string _uploadPath;
    private readonly string _connectionString;

    public DataAnalysisService(AppDbContext context, IAIService aiService, IConfiguration config)
    {
        _context = context;
        _aiService = aiService;
        _uploadPath = config["UploadPath"] ?? "uploads";
        _connectionString = config.GetConnectionString("DefaultConnection") 
            ?? "Data Source=dataanalysis.db";
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
        var columnsJson = JsonSerializer.Serialize(columns);

        var dataset = new Dataset
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileNameWithoutExtension(fileName),
            FilePath = filePath,
            FileType = extension.TrimStart('.'),
            UploadedAt = DateTime.UtcNow,
            ColumnsJson = columnsJson
        };

        _context.Datasets.Add(dataset);
        await _context.SaveChangesAsync();

        // Importa i dati CSV in una tabella SQLite dedicata
        await ImportCsvToSqliteAsync(dataset.Id, filePath, columns);

        return dataset;
    }

    private async Task ImportCsvToSqliteAsync(Guid datasetId, string filePath, List<DataColumn> columns)
    {
        var tableName = $"data_{datasetId:N}";
        
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // Crea tabella dinamica con le colonne del CSV
        var columnDefs = string.Join(", ", columns.Select(c => $"\"{c.Name}\" TEXT"));
        var createTableSql = $"CREATE TABLE IF NOT EXISTS \"{tableName}\" ({columnDefs})";
        
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = createTableSql;
            await cmd.ExecuteNonQueryAsync();
        }

        // Leggi il CSV e inserisci i dati
        var lines = await File.ReadAllLinesAsync(filePath);
        if (lines.Length < 2) return;

        var columnNames = lines[0].Split(',').Select(c => c.Trim()).ToList();
        
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            var values = line.Split(',').Select(v => v.Trim()).ToList();
            var placeholders = string.Join(", ", columnNames.Select(c => $"@{c}"));
            var insertSql = $"INSERT INTO \"{tableName}\" ({string.Join(", ", columnNames.Select(c => $"\"{c}\""))}) VALUES ({placeholders})";
            
            using var cmd = connection.CreateCommand();
            cmd.CommandText = insertSql;
            
            for (int i = 0; i < columnNames.Count; i++)
            {
                cmd.Parameters.AddWithValue($"@{columnNames[i]}", i < values.Count ? values[i] : "");
            }
            
            await cmd.ExecuteNonQueryAsync();
        }
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
        
        var columns = JsonSerializer.Deserialize<List<DataColumn>>(dataset.ColumnsJson) ?? new List<DataColumn>();
        var schema = $"Tabella: {dataset.Name}\nColonne:\n" + 
                     string.Join("\n", columns.Select(c => $"- {c.Name} ({c.DataType})"));
        
        var generatedSQL = await _aiService.GenerateSQLFromNaturalLanguageAsync(userQuery, schema);
        
        // Pulisci la query SQL usando Regex (infallibile contro i markdown)
        generatedSQL = CleanSqlQuery(generatedSQL);
        
        // Esegui la query SQL reale sui dati importati
        var resultData = await ExecuteSqlOnDatasetAsync(datasetId, generatedSQL, columns);
        
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

    // Metodo helper ROBUSTO per pulire la query SQL dai backticks markdown
    private string CleanSqlQuery(string sql)
    {
        if (string.IsNullOrEmpty(sql)) return sql;

        // Usa Regex per estrarre solo il codice SQL dentro i blocchi ```sql ... ```
        var match = Regex.Match(sql, @"```(?:sql)?\s*(.*?)\s*```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        // Fallback: rimuove manualmente i backticks se la regex non trova il pattern completo
        return sql.Replace("```sql", "", StringComparison.OrdinalIgnoreCase)
                  .Replace("```", "")
                  .Trim();
    }

    private async Task<string> ExecuteSqlOnDatasetAsync(Guid datasetId, string sql, List<DataColumn> columns)
    {
        var tableName = $"data_{datasetId:N}";
        
        // Sostituisci il nome tabella generico con quello reale (case-insensitive)
        sql = sql.Replace("tabella", tableName, StringComparison.OrdinalIgnoreCase)
                 .Replace("TABLE", tableName, StringComparison.OrdinalIgnoreCase);

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            
            var results = new List<Dictionary<string, object>>();
            
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var columnName = reader.GetName(i);
                    var value = reader.GetValue(i);
                    row[columnName] = value == DBNull.Value ? null : value.ToString();
                }
                results.Add(row);
            }
            
            return JsonSerializer.Serialize(results);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new[] { new { error = ex.Message } });
        }
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