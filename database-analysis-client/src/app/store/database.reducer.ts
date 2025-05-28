import { createReducer, on } from '@ngrx/store';
import * as DatabaseActions from './database.actions';

export interface DatabaseState {
  stats: any[];
  loading: boolean;
  error: any;
}

export const initialState: DatabaseState = {
  stats: [],
  loading: false,
  error: null
};

export const databaseReducer = createReducer(
  initialState,
  on(DatabaseActions.analyzeDatabase, state => ({ ...state, loading: true, error: null })),
  on(DatabaseActions.analyzeDatabaseSuccess, (state, { stats }) => ({ ...state, loading: false, stats })),
  on(DatabaseActions.analyzeDatabaseFailure, (state, { error }) => ({ ...state, loading: false, error }))
);
