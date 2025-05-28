import { Injectable, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { Observable } from 'rxjs';
import * as DatabaseActions from './store/database.actions';
import { DatabaseState } from './store/database.reducer';

@Injectable({
  providedIn: 'root'
})
export class DatabaseStoreService {
  private store = inject(Store<{ database: DatabaseState }>);

  getStats(): Observable<any[]> {
    return this.store.select((state) => state.database?.stats || []);
  }
  getLoading(): Observable<boolean> {
    return this.store.select((state) => state.database?.loading || false);
  }
  
  getError(): Observable<any> {
    return this.store.select((state) => state.database?.error || null);
  }
  
  dispatchAnalyzeDatabase(databaseServerName: string, databaseName: string): void {
    this.store.dispatch(DatabaseActions.analyzeDatabase({
      databaseServerName,
      databaseName
    }));
  }
}
