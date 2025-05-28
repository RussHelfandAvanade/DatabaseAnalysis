import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { catchError, delay, mergeMap, Observable, of, retryWhen, tap, throwError } from 'rxjs';
import { environment } from '../environments/environment';

@Injectable({ providedIn: 'root' })
export class DatabaseAnalysisService {
  constructor(private http: HttpClient) {
    // Log environment settings on service initialization
    console.log('%c ENVIRONMENT SETTINGS', 'background: red; color: white; font-size: 20px');
    console.log('%c APP VERSION: May 30, 2025 - 10:30', 'background: blue; color: white; font-size: 16px');
    console.log('%c Environment:', 'font-weight: bold; font-size: 16px', environment);
    console.log('%c Current URL:', 'font-weight: bold; font-size: 16px', window.location.href);
    console.log('%c Production Mode:', 'font-weight: bold; font-size: 16px', environment.production);
    console.log('%c Backend API URL:', 'font-weight: bold; font-size: 16px', environment.backendApiUrl);
    
    // Add CORS diagnostic info
    console.log('%c CORS DIAGNOSTICS', 'background: orange; color: black; font-size: 16px');
    console.log('%c Origin:', 'font-weight: bold', window.location.origin);
    console.log('%c Host:', 'font-weight: bold', window.location.host);
    console.log('%c Protocol:', 'font-weight: bold', window.location.protocol);
    
    // Log the build info if it exists
    this.http.get('/assets/build-info.json').subscribe(
      (info) => console.log('%c Build Info:', 'font-weight: bold; font-size: 16px', info),
      (error) => console.log('No build info available:', error)
    );
    
    // Diagnostic test for CORS - preflight check
    this.testCorsPreflightOptions();
  }
  
  // Test CORS preflight with OPTIONS request
  private testCorsPreflightOptions(): void {
    if (!environment.enableDebugLogging) return;
    
    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
      'X-Test-CORS': 'preflight-test'
    });
    
    const backendUrl = environment.backendApiUrl || 'https://databaseanalysisbackend-g4f4dneed4f2f8ad.westus-01.azurewebsites.net';
    const testUrl = `${backendUrl}/analyzedatabase/analyze-database`;
    
    // We'll make a HEAD request to test CORS without actually executing any logic
    this.http.head(testUrl, { headers }).subscribe(
      () => console.log('%c CORS Preflight Test: SUCCESS', 'background: green; color: white'),
      (error) => console.log('%c CORS Preflight Test: FAILED', 'background: red; color: white', error)
    );
  }
  
  analyzeDatabase(databaseServerName: string, databaseName: string): Observable<any> {
    // Set up request headers
    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
      'X-Requested-With': 'XMLHttpRequest',
      'Cache-Control': 'no-cache'
    });
    
    // First try the relative URL approach (which should work with proper Azure routing)
    const relativeUrl = `/analyzedatabase/analyze-database?databaseServerName=${encodeURIComponent(databaseServerName)}&databaseName=${encodeURIComponent(databaseName)}`;
    console.log('Trying relative API URL:', relativeUrl);
    console.log('Current domain:', window.location.origin);
    
    // Report the CORS setup for debugging
    const corsDebugInfo = {
      frontendDomain: window.location.origin,
      timestamp: new Date().toISOString(),
      relativeUrl,
      environment,
      userAgent: navigator.userAgent,
      headers: headers
    };
    console.log('%c CORS DEBUG INFO', 'background: purple; color: white; font-size: 16px', corsDebugInfo);
    
    // First attempt: use relative URL with retry logic for transient errors
    return this.http.get(relativeUrl, { headers }).pipe(
      tap(response => console.log('API Response from relative URL:', response)),      retryWhen(errors => 
        errors.pipe(
          mergeMap((error, i) => {
            // Only retry based on environment configuration
            if (i >= environment.apiRetryAttempts) {
              return throwError(() => error);
            }
            console.log(`Retrying relative URL (${i + 1}/${environment.apiRetryAttempts}) after error:`, error);
            return of(error).pipe(delay(1000)); // Wait 1 second before retrying
          })
        )
      ),      catchError(error => {
        console.error('Relative URL API Error after retries:', error);
        
        // Skip the fallback if direct API calls are disabled
        if (environment.useDirectApi === false) {
          console.log('Direct API calls are disabled in environment settings.');
        }
        
        // Fallback: Use direct backend URL approach
        const backendUrl = environment.backendApiUrl || 'https://databaseanalysisbackend-g4f4dneed4f2f8ad.westus-01.azurewebsites.net';
        const directUrl = `${backendUrl}/analyzedatabase/analyze-database?databaseServerName=${encodeURIComponent(databaseServerName)}&databaseName=${encodeURIComponent(databaseName)}`;
        console.log('Using direct backend URL as fallback:', directUrl);
        
        return this.http.get(directUrl, { headers }).pipe(
          tap(response => console.log('API Response from direct URL:', response)),          retryWhen(errors => 
            errors.pipe(
              mergeMap((directError, i) => {
                // Only retry based on environment configuration
                if (i >= environment.apiRetryAttempts) {
                  return throwError(() => directError);
                }
                console.log(`Retrying direct URL (${i + 1}/${environment.apiRetryAttempts}) after error:`, directError);
                return of(directError).pipe(delay(1000)); // Wait 1 second before retrying
              })
            )
          ),
          catchError(directError => {
            console.error('Direct URL API Error after retries:', directError);
            
            // All attempts failed, return a detailed error object
            return of({ 
              error: 'Both relative and direct API calls failed', 
              relativeError: error, 
              directError,
              time: new Date().toISOString(),
              environment: environment,
              frontend: window.location.origin
            });
          })
        );
      })
    );
  }
}
