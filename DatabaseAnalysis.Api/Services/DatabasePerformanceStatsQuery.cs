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

            // Check if we're running in Azure or locally
            bool isRunningInAzure = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
            Console.WriteLine($"Running in Azure environment: {isRunningInAzure}");

            try
            {
                // Extend the timeout to 2 minutes
                using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                var cancellationToken = cancellationTokenSource.Token;

                // Local development approach - skip Managed Identity if not in Azure
                if (!isRunningInAzure)
                {
                    Console.WriteLine("Local environment detected, attempting developer credentials...");
                    try
                    {
                        // Use interactive browser or Visual Studio credentials for local development
                        var devCredentialOptions = new DefaultAzureCredentialOptions
                        {
                            ExcludeManagedIdentityCredential = true,
                            // Keep interactive credentials for local development
                            ExcludeSharedTokenCacheCredential = true
                        };

                        var devCredential = new DefaultAzureCredential(devCredentialOptions);
                        var tokenRequestContext = new TokenRequestContext(new[] { "https://database.windows.net//.default" });

                        Console.WriteLine("Requesting token for local development...");
                        var token = await devCredential.GetTokenAsync(tokenRequestContext, cancellationToken);
                        Console.WriteLine("Local developer authentication successful");

                        return await ExecuteDatabaseQueryAsync(token.Token, sql, stats, cancellationToken);
                    }
                    catch (Exception localEx)
                    {
                        Console.WriteLine($"Local development authentication failed: {localEx.Message}");

                        // Try SQL authentication if available
                        if (TryGetSqlCredentials(out var username, out var password))
                        {
                            Console.WriteLine("Falling back to SQL authentication for local development");
                            return await ExecuteWithSqlAuthAsync(username, password, sql, stats, cancellationToken);
                        }

                        throw new Exception("Failed to authenticate locally. Please ensure you're logged in with Azure CLI, Visual Studio, or provide SQL credentials.", localEx);
                    }
                }

                // Azure environment approach
                Console.WriteLine("Azure environment detected, attempting Managed Identity authentication...");
                try
                {
                    // Try with system-assigned managed identity
                    var managedCredential = new ManagedIdentityCredential();
                    var tokenRequestContext = new TokenRequestContext(new[] { "https://database.windows.net//.default" });

                    Console.WriteLine("Requesting token using Managed Identity...");
                    var token = await managedCredential.GetTokenAsync(tokenRequestContext, cancellationToken);
                    Console.WriteLine("Managed Identity authentication successful");

                    return await ExecuteDatabaseQueryAsync(token.Token, sql, stats, cancellationToken);
                }
                catch (Exception managedIdEx) when (!(managedIdEx is OperationCanceledException))
                {
                    Console.WriteLine($"Managed Identity authentication failed: {managedIdEx.Message}");
                    Console.WriteLine("Trying DefaultAzureCredential as fallback...");

                    try
                    {
                        // Fall back to DefaultAzureCredential with appropriate exclusions
                        // Fall back to DefaultAzureCredential with appropriate exclusions
                        var credentialOptions = new DefaultAzureCredentialOptions
                        {
                            ExcludeInteractiveBrowserCredential = true,
                            ExcludeAzurePowerShellCredential = true,
                            ExcludeVisualStudioCredential = true,
                            ExcludeAzureCliCredential = true,
                            ExcludeSharedTokenCacheCredential = true,
                            // Set timeout for the entire credential chain
                            Retry = { NetworkTimeout = TimeSpan.FromSeconds(60) }
                        };

                        var credential = new DefaultAzureCredential(credentialOptions);
                        var tokenRequestContext = new TokenRequestContext(new[] { "https://database.windows.net//.default" });

                        Console.WriteLine("Requesting token via DefaultAzureCredential...");
                        var token = await credential.GetTokenAsync(tokenRequestContext, cancellationToken);
                        Console.WriteLine("DefaultAzureCredential authentication successful");

                        return await ExecuteDatabaseQueryAsync(token.Token, sql, stats, cancellationToken);
                    }
                    catch (Exception defaultCredEx) when (!(defaultCredEx is OperationCanceledException))
                    {
                        Console.WriteLine($"DefaultAzureCredential failed: {defaultCredEx.Message}");

                        // Final attempt with SQL auth if available
                        if (TryGetSqlCredentials(out var username, out var password))
                        {
                            Console.WriteLine("All Azure authentication methods failed, trying SQL authentication");
                            return await ExecuteWithSqlAuthAsync(username, password, sql, stats, cancellationToken);
                        }

                        throw new Exception("All authentication methods failed. Please check Managed Identity configuration and network connectivity.", defaultCredEx);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Authentication operation timed out after 2 minutes");
                throw new TimeoutException("Authentication timed out after 2 minutes. This may indicate network connectivity issues or Managed Identity problems.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Authentication error: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");

                throw;
            }
        }

        // Helper method to execute the query using token-based authentication
        private async Task<List<DatabasePerformanceStat>> ExecuteDatabaseQueryAsync(
            string accessToken,
            string sql,
            List<DatabasePerformanceStat> stats,
            CancellationToken cancellationToken)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.AccessToken = accessToken;
                using (var cmd = new SqlCommand(sql, conn))
                {
                    try
                    {
                        Console.WriteLine("Opening SQL connection with token authentication...");
                        await conn.OpenAsync(cancellationToken);
                        Console.WriteLine("SQL connection opened successfully");

                        using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                        {
                            while (await reader.ReadAsync(cancellationToken))
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

                                // Process the query plan filtering
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
                    catch (SqlException sqlEx)
                    {
                        Console.WriteLine($"SQL Exception: {sqlEx.Number} - {sqlEx.Message}");
                        throw;
                    }
                }
            }

            return stats;
        }

        // Helper method to check for SQL credentials
        private bool TryGetSqlCredentials(out string username, out string password)
        {
            username = Environment.GetEnvironmentVariable("SQL_USERNAME") ?? String.Empty;
            password = Environment.GetEnvironmentVariable("SQL_PASSWORD") ?? String.Empty;

            return !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password);
        }

        // Helper method for SQL authentication fallback
        private async Task<List<DatabasePerformanceStat>> ExecuteWithSqlAuthAsync(
            string username,
            string password,
            string sql,
            List<DatabasePerformanceStat> stats,
            CancellationToken cancellationToken)
        {
            var sqlAuthConnString = $"Server=tcp:{_databaseServerName}.database.windows.net,1433;Initial Catalog={_databaseName};Persist Security Info=False;User ID={username};Password={password};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";

            using (var conn = new SqlConnection(sqlAuthConnString))
            {
                using (var cmd = new SqlCommand(sql, conn))
                {
                    try
                    {
                        Console.WriteLine("Opening SQL connection with SQL authentication...");
                        await conn.OpenAsync(cancellationToken);
                        Console.WriteLine("SQL connection opened successfully");

                        using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                        {
                            // Process query results (same as in ExecuteDatabaseQueryAsync)
                            while (await reader.ReadAsync(cancellationToken))
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

                                // Process the query plan filtering (existing code)
                                bool skip = false;
                                if (!string.IsNullOrEmpty(stat.QueryPlan))
                                {
                                    try
                                    {
                                        var xml = XElement.Parse(stat.QueryPlan);
                                        var ns = xml.GetDefaultNamespace();
                                        // Filter logic (same as in ExecuteDatabaseQueryAsync)
                                        var statsInfos = xml.Descendants(ns + "StatisticsInfo");
                                        skip = statsInfos.Any(si =>
                                        {
                                            var tableAttr = si.Attribute("Table");
                                            return tableAttr != null &&
                                                (tableAttr.Value.StartsWith("sys", StringComparison.OrdinalIgnoreCase) ||
                                                 tableAttr.Value.StartsWith("[sys", StringComparison.OrdinalIgnoreCase));
                                        });
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
                    catch (SqlException sqlEx)
                    {
                        Console.WriteLine($"SQL Exception: {sqlEx.Number} - {sqlEx.Message}");
                        throw;
                    }
                }
            }

            return stats;
        }
    }
}
