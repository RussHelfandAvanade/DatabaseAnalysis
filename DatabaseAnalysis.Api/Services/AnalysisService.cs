using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace DatabaseAnalysis.Api.Services
{
    public class AnalysisService
    {
        private readonly IConfiguration _configuration;
        public AnalysisService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<List<DatabasePerformanceStat>> AnalyzeAsync(string databaseServerName, string databaseName)
        {
            var query = new DatabasePerformanceStatsQuery(databaseServerName, databaseName);
            return await query.GetPerformanceStatsAsync();
        }
    }
}
