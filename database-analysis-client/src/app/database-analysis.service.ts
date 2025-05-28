import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { catchError, Observable, tap } from 'rxjs';
import { environment } from '../environments/environment';

@Injectable({ providedIn: 'root' })
export class DatabaseAnalysisService {
  constructor(private http: HttpClient) {}

  analyzeDatabase(databaseServerName: string, databaseName: string): Observable<any> {
    // First try the relative URL approach (which should work with proper Azure routing)
    const relativeUrl = `/analyzedatabase/analyze-database?databaseServerName=${encodeURIComponent(databaseServerName)}&databaseName=${encodeURIComponent(databaseName)}`;
    console.log('Trying relative API URL:', relativeUrl);
    console.log('Current domain:', window.location.origin);
    
    return this.http.get(relativeUrl).pipe(
      tap(response => console.log('API Response from relative URL:', response)),
      catchError(error => {
        console.error('Relative URL API Error:', error);
        
        // If we're in production and the relative URL fails, try direct backend URL as fallback
        if (environment.production && environment.backendApiUrl) {
          const directUrl = `${environment.backendApiUrl}/analyzedatabase/analyze-database?databaseServerName=${encodeURIComponent(databaseServerName)}&databaseName=${encodeURIComponent(databaseName)}`;
          console.log('Trying direct backend URL as fallback:', directUrl);
          
          return this.http.get(directUrl).pipe(
            tap(response => console.log('API Response from direct URL:', response)),
            catchError(directError => {
              console.error('Direct URL API Error:', directError);
              throw directError;
            })
          );
        }
        
        throw error;
      })
    );
  }
}
