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

        await ImportCsvToSqliteAsync(dataset.Id, filePath, columns);

        return dataset;
    }

    private async Task ImportCsvToSqliteAsync(Guid datasetId, string filePath, List<DataColumn> columns)
    {
        var tableName = $"data_{datasetId:N}";
        
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var columnDefs = string.Join(", ", columns.Select(c => $"\"{c.Name}\" TEXT"));
        var createTableSql = $"CREATE TABLE IF NOT EXISTS \"{tableName}\" ({columnDefs})";
        
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = createTableSql;
            await cmd.ExecuteNonQueryAsync();
        }

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
        var tableName = $"data_{dataset.Id:N}";
        
        var columns = JsonSerializer.Deserialize<List<DataColumn>>(dataset.ColumnsJson) ?? new List<DataColumn>();
        
        // PROMPT MIGLIORATO: Istruzioni severe per SQLite
        var schema = $"IMPORTANTE: Usa SOLO sintassi SQLite. NON usare '::numeric', '::integer' o cast PostgreSQL. NON scrivere 'end SQL' o testo extra dopo la query.\n\n" +
                     $"Nome tabella SQL da usare: {tableName}\n\n" +
                     $"Colonne disponibili:\n" + 
                     string.Join("\n", columns.Select(c => $"- {c.Name} (tipo TEXT)"));
        
        var generatedSQL = await _aiService.GenerateSQLFromNaturalLanguageAsync(userQuery, schema);
        
        // Pulizia aggressiva della query
        generatedSQL = CleanSqlQuery(generatedSQL);
        generatedSQL = NormalizeTableName(generatedSQL, tableName);
        generatedSQL = RemovePostgresCast(generatedSQL);
        generatedSQL = RemoveTrailingJunk(generatedSQL);

        // Validazione di sicurezza: solo SELECT
        if (Regex.IsMatch(generatedSQL, @"\b(DELETE|DROP|UPDATE|INSERT|ALTER|CREATE)\b", RegexOptions.IgnoreCase))
        {
            return new QueryResult {
                Id = Guid.NewGuid(), DatasetId = datasetId, UserQuery = userQuery,
                GeneratedSQL = generatedSQL, 
                ResultJSON = JsonSerializer.Serialize(new[] { new { error = "Query non permessa: sono consentite solo query di lettura (SELECT)." } }),
                VisualizationType = "table", ExecutedAt = DateTime.UtcNow
            };
        }

        var resultData = await ExecuteSqlOnDatasetAsync(generatedSQL);
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

    // Rimuove blocchi markdown ```sql ... ```
    private string CleanSqlQuery(string sql)
    {
        if (string.IsNullOrEmpty(sql)) return sql;
        var match = Regex.Match(sql, @"```(?:sql)?\s*(.*?)\s*```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (match.Success) return match.Groups[1].Value.Trim();
        return sql.Replace("```sql", "", StringComparison.OrdinalIgnoreCase).Replace("```", "").Trim();
    }

    // Sostituisce nomi tabella inventati dall'AI
    private string NormalizeTableName(string sql, string realTableName)
    {
        return Regex.Replace(sql, @"\b(tabella|Table|TABLE|Data|data|Dataset|dataset|my_table)\b", realTableName, RegexOptions.IgnoreCase);
    }

    // Rimuove cast PostgreSQL come ::numeric, ::integer, ::text
    private string RemovePostgresCast(string sql)
    {
        // Rimuove "::tipo" (es. ::numeric, ::integer, ::text, ::float)
        return Regex.Replace(sql, @"::\w+", "", RegexOptions.IgnoreCase);
    }

    // Rimuove testo spazzolina alla fine come "end SQL;", "END", ecc.
    private string RemoveTrailingJunk(string sql)
    {
        // Rimuove "end SQL;" o "END SQL" o simili alla fine
        sql = Regex.Replace(sql, @"\bend\s+sql\b\s*;?\s*$", "", RegexOptions.IgnoreCase);
        // Rimuove "END" da solo alla fine
        sql = Regex.Replace(sql, @"\bEND\b\s*;?\s*$", "", RegexOptions.IgnoreCase);
        // Rimuove punti e virgola multipli alla fine
        sql = Regex.Replace(sql, @";+\s*$", ";", RegexOptions.IgnoreCase);
        return sql.Trim();
    }

    private async Task<string> ExecuteSqlOnDatasetAsync(string sql)
    {
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