export const environment = {
  production: false,
  backendApiUrl: '',  // Empty for local development as we use proxy.conf.json
  apiRetryAttempts: 1, 
  apiTimeoutMs: 10000, // 10 seconds
  useDirectApi: false,
  enableDebugLogging: true,
  buildDate: '2025-05-28'
};
