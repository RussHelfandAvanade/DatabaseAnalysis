import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { FormControl, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';
import { DatabaseStoreService } from './database-store.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  template: `
    <div class="container">
      <h1>Database Analysis</h1>
      <form (ngSubmit)="analyze()" class="form">
        <div class="form-group">
          <label>Azure SQL Server Name
            <input [formControl]="databaseServerName" required placeholder="myazuresqlserver" />
          </label>
        </div>
        <div class="form-group">
          <label>Database Name
            <input [formControl]="databaseName" required placeholder="mydatabasename" />
          </label>
        </div>
        <button type="submit" class="analyze-btn">Analyze Database</button>
      </form>
      <div *ngIf="loading$ | async" class="loading"><span class="spinner"></span> Loading...</div>
      <div *ngIf="error$ | async as error" class="error">
        ⚠️ {{ getErrorMessage(error) }}
      </div>
      <ng-container *ngIf="stats$ | async as stats">
        <table class="perf-table" *ngIf="$any(stats)?.length > 0">
          <thead>
            <tr>
              <th>Server</th>
              <th>Database</th>
              <th>Total Logical Reads</th>
              <th>Total Logical Writes</th>
              <th>Execution Count</th>
              <th>IO Total</th>
              <th>Avg CPU Time</th>
              <th>Statement Text</th>
              <th>Query Plan</th>
              <th>SQL Handle</th>
              <th>Plan Handle</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let stat of $any(stats); let i = index">
              <td>{{stat.serverName}}</td>
              <td>{{stat.databaseName}}</td>
              <td>{{stat.totalLogicalReads}}</td>
              <td>{{stat.totalLogicalWrites}}</td>
              <td>{{stat.executionCount}}</td>
              <td>{{stat.ioTotal}}</td>
              <td>{{stat.avgCpuTime}}</td>
              <td class="statement-text">
                <span [title]="stat.statementText">
                  {{ stat.statementText.length > 50 ? (stat.statementText | slice:0:50) + '…' : stat.statementText }}
                </span>
                <button class="show-full-btn show-all-block" (click)="openModal('Statement Text', stat.statementText, i, $event)">Show All</button>
              </td>
              <td class="query-plan-cell">
                <span [title]="stat.queryPlan">
                  {{ stat.queryPlan.length > 50 ? (stat.queryPlan | slice:0:50) + '…' : stat.queryPlan }}
                </span>
                <button class="show-full-btn show-all-block" (click)="openModal('Query Plan', stat.queryPlan, i, $event)">Show All</button>
              </td>
              <td class="handle">{{stat.sqlHandle}}</td>
              <td class="handle">
                <span [title]="stat.planHandle">
                  {{ stat.planHandle.length > 50 ? (stat.planHandle | slice:0:50) + '…' : stat.planHandle }}
                </span>
              </td>
            </tr>
          </tbody>
        </table>
        <div *ngIf="$any(stats)?.length === 0" class="no-results">No results found.</div>
      </ng-container>

      <!-- Modal Dialog -->
      <div class="modal-backdrop" *ngIf="modalOpen" (click)="closeModal()"></div>
      <div class="modal" *ngIf="modalOpen">
        <div class="modal-header">
          <span class="modal-title">{{modalTitle}}</span>
          <button class="modal-close" (click)="closeModal()">&times;</button>
        </div>
        <div class="modal-body">
          <pre class="modal-content">{{modalValue}}</pre>
          <button class="clipboard-btn" (click)="copyToClipboard(modalValue)">Copy to Clipboard</button>
          <span *ngIf="clipboardSuccess" class="clipboard-success">Copied!</span>
        </div>
      </div>

      <!-- Debug Info -->
      <div class="debug-info">
        <h2>Debug Information</h2>
        <p><strong>Current Domain:</strong> {{ debugInfo.currentDomain }}</p>
        <p><strong>Build Time:</strong> {{ debugInfo.buildTime }}</p>
        <p><strong>Environment:</strong> {{ debugInfo.environment.production ? 'Production' : 'Development' }}</p>
      </div>
    </div>
  `,
  styles: [
    `
      * {
        font-family: Arial, Helvetica, sans-serif !important;
        box-sizing: border-box;
      }
      body, html {
        font-family: Arial, Helvetica, sans-serif !important;
        background: #f4f6fa;
        margin: 0;
        padding: 0;
      }
      .container {
        max-width: 1100px;
        margin: 2.5rem auto;
        background: #fff;
        border-radius: 18px;
        box-shadow: 0 6px 32px 0 rgba(44,62,80,0.10);
        padding: 2.7rem 2.2rem 2.2rem 2.2rem;
      }
      h1 {
        font-size: 2.4rem;
        font-weight: 700;
        margin-bottom: 2.2rem;
        color: #1a2330;
        letter-spacing: 0.5px;
        font-family: Arial, Helvetica, sans-serif;
      }
      .form {
        display: flex;
        gap: 2.2rem;
        align-items: flex-end;
        margin-bottom: 2.2rem;
        flex-wrap: wrap;
      }
      .form-group {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
      }
      label {
        font-weight: 600;
        color: #2d3a4a;
        margin-bottom: 0.2rem;
        font-size: 1.08rem;
      }
      input[type="text"], input[type="email"] {
        padding: 0.7rem 1.1rem;
        border: 1.5px solid #d1d5db;
        border-radius: 9px;
        font-size: 1.05rem;
        background: #f8fafc;
        transition: border 0.2s;
        outline: none;
        font-family: Arial, Helvetica, sans-serif;
      }
      input[type="text"]:focus, input[type="email"]:focus {
        border: 2px solid #4f8cff;
        background: #fff;
      }
      .analyze-btn {
        background: linear-gradient(90deg, #4f8cff 0%, #6f6fff 100%);
        color: #fff;
        font-weight: 700;
        border: none;
        border-radius: 9px;
        padding: 0.8rem 2.4rem;
        font-size: 1.13rem;
        cursor: pointer;
        box-shadow: 0 2px 10px 0 rgba(79,140,255,0.10);
        transition: background 0.2s, box-shadow 0.2s;
        font-family: Arial, Helvetica, sans-serif;
      }
      .analyze-btn:hover {
        background: linear-gradient(90deg, #3a6fd8 0%, #5a5ad8 100%);
        box-shadow: 0 4px 18px 0 rgba(79,140,255,0.18);
      }
      .loading {
        color: #4f8cff;
        font-weight: 600;
        margin-bottom: 1.2rem;
        display: flex;
        align-items: center;
        gap: 0.7rem;
        font-size: 1.1rem;
      }
      .spinner {
        width: 20px;
        height: 20px;
        border: 3px solid #e3e8ee;
        border-top: 3px solid #4f8cff;
        border-radius: 50%;
        animation: spin 1s linear infinite;
        display: inline-block;
      }
      @keyframes spin {
        0% { transform: rotate(0deg); }
        100% { transform: rotate(360deg); }
      }
      .error {
        color: #d32f2f;
        background: #fff0f0;
        border: 1.5px solid #ffd6d6;
        border-radius: 7px;
        padding: 0.8rem 1.3rem;
        margin-bottom: 1.2rem;
        font-weight: 600;
        display: inline-block;
        font-size: 1.08rem;
      }
      .no-results {
        color: #888;
        font-style: italic;
        margin-top: 1.7rem;
        text-align: center;
        font-size: 1.08rem;
      }
      .perf-table {
        border-collapse: separate;
        border-spacing: 0;
        width: 100%;
        margin-top: 1.2rem;
        background: #f8fafc;
        border-radius: 12px;
        overflow: hidden;
        box-shadow: 0 2px 12px 0 rgba(44,62,80,0.06);
        font-size: 1.03rem;
      }
      .perf-table th, .perf-table td {
        border: 1px solid #e3e8ee;
        padding: 0.85rem 0.7rem;
        text-align: left;
        font-size: 1.03rem;
        font-family: Arial, Helvetica, sans-serif;
      }
      .perf-table th {
        background: #eaf1fb;
        color: #1a2330;
        font-weight: 700;
        font-size: 1.08rem;
        letter-spacing: 0.02em;
      }
      .perf-table td {
        background: #fff;
        color: #2d3a4a;
        vertical-align: top;
      }
      .statement-text, .query-plan-cell {
        max-width: 420px;
        overflow-x: auto;
        white-space: pre-line;
        font-family: Arial, Helvetica, sans-serif;
        font-size: 1.01rem;
        background: #f4f7fa;
        border-radius: 5px;
        padding: 0.25rem 0.5rem 0.5rem 0.5rem;
        margin-bottom: 0.1rem;
      }
      .handle {
        max-width: 140px;
        overflow-x: auto;
        font-family: Arial, Helvetica, sans-serif;
        font-size: 0.99rem;
        background: #f4f7fa;
        border-radius: 4px;
        padding: 0.2rem 0.4rem;
      }
      .show-full-btn.show-all-block {
        display: block;
        margin-top: 0.4em;
        width: 100%;
        text-align: left;
        background: linear-gradient(90deg, #4f8cff 0%, #6f6fff 100%);
        color: #fff;
        font-weight: 600;
        border: none;
        border-radius: 6px;
        padding: 0.5rem 1.2rem;
        font-size: 1.01rem;
        cursor: pointer;
        margin-bottom: 0.2rem;
        transition: background 0.2s;
        font-family: Arial, Helvetica, sans-serif;
      }
      .show-full-btn.show-all-block:hover {
        background: linear-gradient(90deg, #3a6fd8 0%, #5a5ad8 100%);
      }
      .modal-backdrop {
        position: fixed;
        top: 0; left: 0; right: 0; bottom: 0;
        background: rgba(44,62,80,0.22);
        z-index: 1000;
        display: block;
      }
      .modal {
        position: fixed;
        top: 50%; left: 50%;
        transform: translate(-50%, -50%);
        background: #fff;
        border-radius: 14px;
        box-shadow: 0 10px 40px 0 rgba(44,62,80,0.22);
        z-index: 1001;
        min-width: 370px;
        max-width: 92vw;
        min-height: 120px;
        max-height: 82vh;
        display: block;
        padding: 0;
        overflow: hidden;
        animation: modalIn 0.18s cubic-bezier(.4,1.3,.6,1) 1;
        font-family: Arial, Helvetica, sans-serif;
      }
      @keyframes modalIn {
        from { opacity: 0; transform: translate(-50%, -60%); }
        to { opacity: 1; transform: translate(-50%, -50%); }
      }
      .modal-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        background: #eaf1fb;
        padding: 1.1rem 1.3rem 0.8rem 1.3rem;
        border-bottom: 1.5px solid #e3e8ee;
      }
      .modal-title {
        font-weight: 700;
        font-size: 1.13rem;
        color: #1a2330;
        font-family: Arial, Helvetica, sans-serif;
      }
      .modal-close {
        background: none;
        border: none;
        font-size: 1.7rem;
        color: #888;
        cursor: pointer;
        transition: color 0.2s;
        font-family: Arial, Helvetica, sans-serif;
      }
      .modal-close:hover {
        color: #d32f2f;
      }
      .modal-body {
        padding: 1.3rem 1.3rem 1.7rem 1.3rem;
        display: flex;
        flex-direction: column;
        align-items: flex-start;
        font-family: Arial, Helvetica, sans-serif;
      }
      .modal-content {
        font-family: Arial, Helvetica, sans-serif;
        font-size: 1.05rem;
        background: #f8fafc;
        border-radius: 7px;
        padding: 0.8rem 1.1rem;
        margin-bottom: 1.1rem;
        max-width: 64vw;
        max-height: 44vh;
        overflow-x: auto;
        white-space: pre-wrap;
        word-break: break-all;
      }
      .clipboard-btn {
        background: linear-gradient(90deg, #4f8cff 0%, #6f6fff 100%);
        color: #fff;
        font-weight: 600;
        border: none;
        border-radius: 7px;
        padding: 0.6rem 1.4rem;
        font-size: 1.01rem;
        cursor: pointer;
        margin-bottom: 0.2rem;
        transition: background 0.2s;
        font-family: Arial, Helvetica, sans-serif;
      }
      .clipboard-btn:hover {
        background: linear-gradient(90deg, #3a6fd8 0%, #5a5ad8 100%);
      }
      .clipboard-success {
        color: #4caf50;
        font-size: 1.01rem;
        margin-left: 0.7em;
        font-weight: 600;
        transition: opacity 0.2s;
        font-family: Arial, Helvetica, sans-serif;
      }
      .debug-info {
        margin-top: 2rem;
        padding: 1.5rem;
        background: #f4f7fa;
        border-radius: 8px;
        box-shadow: 0 2px 10px 0 rgba(44,62,80,0.08);
        font-size: 0.95rem;
        color: #333;
        font-family: Arial, Helvetica, sans-serif;
      }
      .debug-info h2 {
        font-size: 1.2rem;
        font-weight: 700;
        margin-bottom: 1rem;
        color: #1a2330;
      }
      .debug-info p {
        margin: 0.4rem 0;
      }
      @media (max-width: 900px) {
        .form { flex-direction: column; gap: 1.2rem; }
        .container { padding: 1.2rem; }
        .perf-table th, .perf-table td { font-size: 0.97rem; }
        .modal { min-width: 90vw; }
      }
    `
  ]
})
export class AppComponent implements OnInit {
  databaseServerName = new FormControl('');
  databaseName = new FormControl('');
    private dbStore = inject(DatabaseStoreService);
  
  stats$: Observable<any[]> = this.dbStore.getStats();
  loading$: Observable<boolean> = this.dbStore.getLoading();
  error$: Observable<any> = this.dbStore.getError();

  modalOpen = false;
  modalTitle = '';
  modalValue = '';
  clipboardSuccess = false;

  debugInfo = {
    currentDomain: '',
    buildTime: new Date().toISOString(),
    environment: { ...environment }
  };

  ngOnInit() {
    this.debugInfo.currentDomain = window.location.origin;
    console.log('Debug Info:', this.debugInfo);
  }

  analyze() {
    if (this.databaseServerName.value && this.databaseName.value) {
      this.dbStore.dispatchAnalyzeDatabase(
        this.databaseServerName.value,
        this.databaseName.value
      );
    }
  }

  openModal(title: string, value: string, rowIndex: number, event: Event) {
    event.stopPropagation();
    this.modalTitle = title;
    this.modalValue = value;
    this.modalOpen = true;
    this.clipboardSuccess = false;
  }

  closeModal() {
    this.modalOpen = false;
    this.modalTitle = '';
    this.modalValue = '';
    this.clipboardSuccess = false;
  }

  async copyToClipboard(value: string) {
    try {
      await navigator.clipboard.writeText(value);
      this.clipboardSuccess = true;
      setTimeout(() => (this.clipboardSuccess = false), 1200);
    } catch {
      this.clipboardSuccess = false;
    }
  }

  getErrorMessage(error: any): string {
    if (!error) return '';
    if (typeof error === 'string') return error;
    if (typeof error === 'object') {
      if (error.message) return error.message;
      try {
        return JSON.stringify(error);
      } catch {
        return 'An unknown error occurred.';
      }
    }
    return String(error);
  }
}