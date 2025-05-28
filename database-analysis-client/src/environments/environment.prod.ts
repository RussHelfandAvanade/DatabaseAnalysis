export const environment = {
  production: true,
  backendApiUrl: 'https://databaseanalysisbackend-g4f4dneed4f2f8ad.westus-01.azurewebsites.net',
  apiRetryAttempts: 2,
  apiTimeoutMs: 30000, // 30 seconds
  useDirectApi: false, // Set to true to bypass SWA routing and call backend directly
  enableDebugLogging: true,
  buildDate: '2025-05-28'
};
