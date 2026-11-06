using Application.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DataAnalysisController : ControllerBase
{
    private readonly IDataAnalysisService _dataService;

    public DataAnalysisController(IDataAnalysisService dataService)
    {
        _dataService = dataService;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadDataset(IFormFile file)
    {
        if (file == null || file.Length == 0) 
            return BadRequest("Nessun file caricato");
        
        using var stream = file.OpenReadStream();
        var dataset = await _dataService.UploadDatasetAsync(file.FileName, stream);
        
        // Deserializziamo la stringa JSON per restituire un array pulito al frontend
        var columns = JsonSerializer.Deserialize<List<DataColumn>>(dataset.ColumnsJson) ?? new List<DataColumn>();
        
        return Ok(new { 
            message = "Dataset caricato!", 
            datasetId = dataset.Id, 
            columns = columns 
        });
    }

    [HttpGet("datasets")]
    public async Task<IActionResult> GetDatasets() 
    {
        var datasets = await _dataService.GetAllDatasetsAsync();
        return Ok(datasets);
    }

    [HttpPost("query")]
    public async Task<IActionResult> ExecuteQuery(
        [FromQuery] Guid datasetId, 
        [FromBody] string query)
    {
        var result = await _dataService.ExecuteNaturalQueryAsync(datasetId, query);
        return Ok(result);
    }
}