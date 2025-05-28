import { createAction, props } from '@ngrx/store';

export const analyzeDatabase = createAction(
  '[Database] Analyze Database',
  props<{ databaseServerName: string; databaseName: string }>() // Removed userEmail, only databaseServerName and databaseName are required
);

export const analyzeDatabaseSuccess = createAction(
  '[Database] Analyze Database Success',
  props<{ stats: any[] }>()
);

export const analyzeDatabaseFailure = createAction(
  '[Database] Analyze Database Failure',
  props<{ error: any }>()
);
