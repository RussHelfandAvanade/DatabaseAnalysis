using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using System;
using System.Xml.Linq;
using System.Linq;
using Azure.Identity;
using Azure.Core;

namespace DatabaseAnalysis.Api.Services
{
    public class DatabasePerformanceStat
    {
        public string ServerName { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public long TotalLogicalReads { get; set; }
        public long TotalLogicalWrites { get; set; }
        public int ExecutionCount { get; set; }
        public long IoTotal { get; set; }
        public long AvgCpuTime { get; set; }
        public string StatementText { get; set; } = string.Empty;
        public string SqlHandle { get; set; } = string.Empty;
        public string PlanHandle { get; set; } = string.Empty;
        public string QueryPlan { get; set; } = string.Empty;
    }

    public class DatabasePerformanceStatsQuery
    {
        private readonly string _connectionString;
        private readonly string _databaseServerName;
        private readonly string _databaseName;

        public DatabasePerformanceStatsQuery(string databaseServerName, string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseServerName))
                throw new ArgumentException("A valid database server name must be provided to DatabasePerformanceStatsQuery.", nameof(databaseServerName));
            if (string.IsNullOrWhiteSpace(databaseName))
                throw new ArgumentException("A valid database name must be provided to DatabasePerformanceStatsQuery.", nameof(databaseName));
            _databaseServerName = databaseServerName;
            _databaseName = databaseName;
            _connectionString = $"Server=tcp:{databaseServerName}.database.windows.net,1433;Initial Catalog={databaseName};Persist Security Info=False;Encrypt=True;TrustServerCertificate=False;Connect Timeout=120;";
        }

        public async Task<List<DatabasePerformanceStat>> GetPerformanceStatsAsync()
        {
            var stats = new List<DatabasePerformanceStat>();
            var sql = @"
SET NOCOUNT ON;
WITH TopConsumerQueries_CTE (TOTAL_LOGICAL_READS, TOTAL_LOGICAL_WRITES, EXECUTION_COUNT, IO_TOTAL, AvgCPUTime, statement_text, sql_handle, plan_handle) AS
(
    SELECT TOP 20 
        TOTAL_LOGICAL_READS, 
        TOTAL_LOGICAL_WRITES, 
        EXECUTION_COUNT, 
        TOTAL_LOGICAL_READS + TOTAL_LOGICAL_WRITES AS [IO_TOTAL], 
        total_worker_time / execution_count AS [AvgCPUTime], 
        st.text AS statement_text, 
        qs.sql_handle AS sql_handle, 
        qs.plan_handle 
    FROM SYS.DM_EXEC_QUERY_STATS qs 
    CROSS APPLY SYS.DM_EXEC_SQL_TEXT(qs.sql_handle) st
    ORDER BY total_worker_time / execution_count DESC
)
SELECT 
    @@servername AS servername, 
    db_name(qp.dbid) AS database_name, 
    TOTAL_LOGICAL_READS, 
    TOTAL_LOGICAL_WRITES, 
    EXECUTION_COUNT, 
    IO_TOTAL, 
    AvgCPUTime, 
    statement_text, 
    CONVERT(VARCHAR(MAX), sql_handle, 1) AS sql_handle, 
    CONVERT(VARCHAR(MAX), plan_handle, 1) AS plan_handle, 
    qp.query_plan
FROM TopConsumerQueries_CTE 
CROSS APPLY SYS.DM_EXEC_QUERY_PLAN (TopConsumerQueries_CTE.plan_handle) AS qp
WHERE qp.query_plan IS NOT NULL 
  AND db_name(qp.dbid) IS NOT NULL;";

            // Use Azure.Identity's InteractiveBrowserCredential for interactive login
            var credential = new InteractiveBrowserCredential();
            var tokenRequestContext = new TokenRequestContext(new[] { "https://database.windows.net//.default" });
            AccessToken token = await credential.GetTokenAsync(tokenRequestContext);

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.AccessToken = token.Token;
                using (var cmd = new SqlCommand(sql, conn))
                {
                    await conn.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var stat = new DatabasePerformanceStat
                            {
                                ServerName = reader["servername"] as string ?? string.Empty,
                                DatabaseName = reader["database_name"] as string ?? string.Empty,
                                TotalLogicalReads = reader["TOTAL_LOGICAL_READS"] as long? ?? 0,
                                TotalLogicalWrites = reader["TOTAL_LOGICAL_WRITES"] as long? ?? 0,
                                ExecutionCount = reader["EXECUTION_COUNT"] != DBNull.Value ? Convert.ToInt32(reader["EXECUTION_COUNT"]) : 0,
                                IoTotal = reader["IO_TOTAL"] as long? ?? 0,
                                AvgCpuTime = reader["AvgCPUTime"] as long? ?? 0,
                                StatementText = reader["statement_text"] as string ?? string.Empty,
                                SqlHandle = reader["sql_handle"] as string ?? string.Empty,
                                PlanHandle = reader["plan_handle"] as string ?? string.Empty,
                                QueryPlan = reader["query_plan"] as string ?? string.Empty
                            };
                            // Exclude if query_plan XML has a StatisticsInfo element with Table attribute starting with sys or [sys
                            bool skip = false;
                            if (!string.IsNullOrEmpty(stat.QueryPlan))
                            {
                                try
                                {
                                    var xml = XElement.Parse(stat.QueryPlan);
                                    var ns = xml.GetDefaultNamespace();
                                    // Existing logic: skip if StatisticsInfo Table attribute starts with sys or [sys
                                    var statsInfos = xml.Descendants(ns + "StatisticsInfo");
                                    skip = statsInfos.Any(si =>
                                    {
                                        var tableAttr = si.Attribute("Table");
                                        return tableAttr != null &&
                                            (tableAttr.Value.StartsWith("sys", StringComparison.OrdinalIgnoreCase) ||
                                             tableAttr.Value.StartsWith("[sys", StringComparison.OrdinalIgnoreCase));
                                    });
                                    // New logic: skip if any StmtSimple element's StatementText contains " sys."
                                    if (!skip)
                                    {
                                        var stmtSimples = xml.Descendants(ns + "StmtSimple");
                                        skip = stmtSimples.Any(stmt =>
                                        {
                                            var stmtTextAttr = stmt.Attribute("StatementText");
                                            return stmtTextAttr != null && stmtTextAttr.Value.Contains(" sys.", StringComparison.OrdinalIgnoreCase);
                                        });
                                    }
                                }
                                catch { /* ignore XML parse errors, do not skip */ }
                            }
                            if (!skip)
                                stats.Add(stat);
                        }
                    }
                }
            }
            return stats;
        }
    }
}
