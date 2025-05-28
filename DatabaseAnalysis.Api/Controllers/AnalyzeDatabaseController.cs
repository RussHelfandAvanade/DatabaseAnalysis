using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using DatabaseAnalysis.Api.Services;

namespace DatabaseAnalysis.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AnalyzeDatabaseController : ControllerBase
    {
        private readonly AnalysisService _analysisService;

        public AnalyzeDatabaseController(AnalysisService analysisService)
        {
            _analysisService = analysisService;
        }

        [HttpGet("analyze-database")]
        public async Task<IActionResult> AnalyzeDatabase([FromQuery] string databaseServerName, [FromQuery] string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseServerName))
                return BadRequest("A valid database server name must be provided.");
            if (string.IsNullOrWhiteSpace(databaseName))
                return BadRequest("A valid database name must be provided.");

            var result = await _analysisService.AnalyzeAsync(databaseServerName, databaseName);
            return Ok(result);
        }
    }
}
