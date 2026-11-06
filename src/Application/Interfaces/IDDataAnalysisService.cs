using Domain.Entities;

namespace Application.Interfaces;

public interface IDataAnalysisService
{
    Task<Dataset> UploadDatasetAsync(string fileName, Stream fileStream);
    Task<List<Dataset>> GetAllDatasetsAsync();
    Task<QueryResult> ExecuteNaturalQueryAsync(Guid datasetId, string userQuery);
    Task<Dataset> GetDatasetSchemaAsync(Guid datasetId);
}