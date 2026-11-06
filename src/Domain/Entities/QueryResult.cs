namespace Domain.Entities;

public class QueryResult
{
    public Guid Id { get; set; }
    public Guid DatasetId { get; set; }
    public string UserQuery { get; set; } = string.Empty;
    public string GeneratedSQL { get; set; } = string.Empty;
    public string ResultJSON { get; set; } = string.Empty;
    public string? VisualizationType { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}
