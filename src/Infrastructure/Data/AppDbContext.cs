using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Dataset> Datasets { get; set; }
    public DbSet<QueryResult> QueryResults { get; set; }
    
    // EF Core configurerà automaticamente "Id" come chiave primaria per entrambe le tabelle
}