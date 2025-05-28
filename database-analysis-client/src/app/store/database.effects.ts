import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { of } from 'rxjs';
import { catchError, map, mergeMap } from 'rxjs/operators';
import { DatabaseAnalysisService } from '../database-analysis.service';
import * as DatabaseActions from './database.actions';

@Injectable()
export class DatabaseEffects {
  private actions$ = inject(Actions);
  private databaseAnalysisService = inject(DatabaseAnalysisService);

  analyzeDatabase$ = createEffect(() =>
    this.actions$.pipe(
      ofType(DatabaseActions.analyzeDatabase),
      mergeMap(action =>
        this.databaseAnalysisService.analyzeDatabase(action.databaseServerName, action.databaseName).pipe(
          map(result => DatabaseActions.analyzeDatabaseSuccess({ stats: result as any[] })),
          catchError(error => of(DatabaseActions.analyzeDatabaseFailure({ error })))
        )
      )
    )
  );
}