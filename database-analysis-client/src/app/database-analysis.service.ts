import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { catchError, Observable, of, tap } from 'rxjs';
import { environment } from '../environments/environment';

@Injectable({ providedIn: 'root' })
export class DatabaseAnalysisService {
  constructor(private http: HttpClient) {
    // Log environment settings on service initialization
    console.log('%c ENVIRONMENT SETTINGS', 'background: red; color: white; font-size: 20px');
    console.log('%c APP VERSION: May 28, 2025 - 16:30', 'background: blue; color: white; font-size: 16px');
    console.log('%c Environment:', 'font-weight: bold; font-size: 16px', environment);
    console.log('%c Current URL:', 'font-weight: bold; font-size: 16px', window.location.href);
    console.log('%c Production Mode:', 'font-weight: bold; font-size: 16px', environment.production);
    console.log('%c Backend API URL:', 'font-weight: bold; font-size: 16px', environment.backendApiUrl);
  }

  analyzeDatabase(databaseServerName: string, databaseName: string): Observable<any> {
    // First try the relative URL approach (which should work with proper Azure routing)
    const relativeUrl = `/analyzedatabase/analyze-database?databaseServerName=${encodeURIComponent(databaseServerName)}&databaseName=${encodeURIComponent(databaseName)}`;
    console.log('Trying relative API URL:', relativeUrl);
    console.log('Current domain:', window.location.origin);
    
    return this.http.get(relativeUrl).pipe(
      tap(response => console.log('API Response from relative URL:', response)),
      catchError(error => {
        console.error('Relative URL API Error:', error);
        
        // IMPORTANT: Using unconditional fallback to direct URL, regardless of environment
        // Hardcoded URL for Azure backend
        const backendUrl = environment.backendApiUrl || 'https://databaseanalysisbackend-g4f4dneed4f2f8ad.westus-01.azurewebsites.net';
        const directUrl = `${backendUrl}/analyzedatabase/analyze-database?databaseServerName=${encodeURIComponent(databaseServerName)}&databaseName=${encodeURIComponent(databaseName)}`;
        console.log('Using direct fallback regardless of environment.production value');
        console.log('Trying direct backend URL as fallback:', directUrl);
        
        return this.http.get(directUrl).pipe(
          tap(response => console.log('API Response from direct URL:', response)),
          catchError(directError => {
            console.error('Direct URL API Error:', directError);
            return of({ error: 'Both relative and direct API calls failed', relativeError: error, directError });
          })
        );
      })
    );
  }
}
