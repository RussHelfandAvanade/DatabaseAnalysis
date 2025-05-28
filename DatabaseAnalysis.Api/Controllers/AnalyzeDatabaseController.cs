using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using DatabaseAnalysis.Api.Services;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace DatabaseAnalysis.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AnalyzeDatabaseController : ControllerBase
    {
        private readonly AnalysisService _analysisService;
        private readonly ILogger<AnalyzeDatabaseController> _logger;

        public AnalyzeDatabaseController(AnalysisService analysisService, ILogger<AnalyzeDatabaseController> logger)
        {
            _analysisService = analysisService;
            _logger = logger;
        }

        [HttpGet("analyze-database")]
        public async Task<IActionResult> AnalyzeDatabase([FromQuery] string databaseServerName, [FromQuery] string databaseName)
        {
            // Log the request for debugging
            _logger.LogInformation($"Analyze database request received. Server: {databaseServerName}, Database: {databaseName}");
            _logger.LogInformation($"Request origin: {Request.Headers["Origin"]}");
            _logger.LogInformation($"Request host: {Request.Host}");

            if (string.IsNullOrWhiteSpace(databaseServerName))
                return BadRequest("A valid database server name must be provided.");
            if (string.IsNullOrWhiteSpace(databaseName))
                return BadRequest("A valid database name must be provided.");

            var result = await _analysisService.AnalyzeAsync(databaseServerName, databaseName);
            return Ok(result);
        }

        [HttpGet("cors-test")]
        public IActionResult CorsTest()
        {
            // Log CORS diagnostic information
            _logger.LogInformation("CORS Test endpoint called");
            _logger.LogInformation($"Request origin: {Request.Headers["Origin"]}");
            _logger.LogInformation($"Request host: {Request.Host}");

            // Return CORS diagnostic information
            var diagnosticInfo = new Dictionary<string, object>
            {
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["origin"] = Request.Headers["Origin"].ToString(),
                ["host"] = Request.Host.ToString(),
                ["method"] = Request.Method,
                ["scheme"] = Request.Scheme,
                ["path"] = Request.Path.ToString(),
                ["userAgent"] = Request.Headers["User-Agent"].ToString(),
                ["referer"] = Request.Headers["Referer"].ToString(),
                ["headers"] = Request.Headers.Keys.ToDictionary(k => k, k => Request.Headers[k].ToString())
            };

            return Ok(diagnosticInfo);
        }

        [HttpOptions("analyze-database")]
        public IActionResult HandlePreflight()
        {
            _logger.LogInformation("OPTIONS preflight request received");
            _logger.LogInformation($"Request origin: {Request.Headers["Origin"]}");

            // Respond properly to CORS preflight requests
            return Ok();
        }
    }
}
