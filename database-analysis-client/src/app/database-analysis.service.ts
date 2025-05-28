import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class DatabaseAnalysisService {
  constructor(private http: HttpClient) {}

  analyzeDatabase(databaseServerName: string, databaseName: string): Observable<any> {
    return this.http.get(`/analyzedatabase/analyze-database?databaseServerName=${encodeURIComponent(databaseServerName)}&databaseName=${encodeURIComponent(databaseName)}`);
  }
}
