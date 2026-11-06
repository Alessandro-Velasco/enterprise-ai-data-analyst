namespace Domain.Entities;

public class Dataset
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    
    // ATTENZIONE: Deve essere ColumnsJson (stringa), NON Columns (lista)
    public string ColumnsJson { get; set; } = "[]";
}

public class DataColumn
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
}