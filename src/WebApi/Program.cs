using Application.Interfaces;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Database SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=dataanalysis.db"));

// 2. Servizi
builder.Services.AddScoped<IDataAnalysisService, DataAnalysisService>();
builder.Services.AddScoped<IAIService, AIService>();

// 3. CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact", policy =>
        policy.WithOrigins("http://localhost:5173").AllowAnyMethod().AllowAnyHeader());
});

// 4. CONTROLLERS - QUESTA RIGA È FONDAMENTALE!
builder.Services.AddControllers();

// 5. Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Crea il database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseCors("AllowReact");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 6. QUESTA RIGA È FONDAMENTALE!
app.MapControllers();

// Porta fissa
app.Urls.Add("http://localhost:5000");

app.Run();