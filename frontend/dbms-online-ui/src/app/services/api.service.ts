import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  ConnectionRequest,
  ConnectionResponse,
  DatabaseSchema,
  TableSchema,
  ColumnSchema,
  StoredProcedure,
  TableRelation,
  QueryRequest,
  QueryResult,
  DataPageRequest,
  DataPageResult,
  RowMutationRequest,
  AuditLog,
  ExportTableRequest,
  ExportDatabaseRequest,
  ExportResult,
  ImportTableRequest,
  ImportDatabaseRequest,
  ImportResult,
  TelemetryMetrics,
  DashboardAnalytics,
  Report,
  CreateReportRequest,
  CreateBackupRequest,
  RestoreDatabaseRequest,
  DatabaseBackup,
  BackupResult,
  RestoreResult
} from '../models/dbms.models';

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private readonly baseUrl = 'http://localhost:5123/api';

  constructor(private http: HttpClient) {}

  // Connection API
  getConnections(): Observable<ConnectionResponse[]> {
    return this.http.get<ConnectionResponse[]>(`${this.baseUrl}/connections`);
  }

  saveConnection(request: ConnectionRequest): Observable<ConnectionResponse> {
    return this.http.post<ConnectionResponse>(`${this.baseUrl}/connections`, request);
  }

  testConnection(request: ConnectionRequest): Observable<{ success: boolean; message: string }> {
    return this.http.post<{ success: boolean; message: string }>(`${this.baseUrl}/connections/test`, request);
  }

  deleteConnection(id: string): Observable<boolean> {
    return this.http.delete<boolean>(`${this.baseUrl}/connections/${id}`);
  }

  // Schema API
  getDatabases(connectionId: string): Observable<DatabaseSchema> {
    return this.http.get<DatabaseSchema>(`${this.baseUrl}/schema/databases/${connectionId}`);
  }

  getTables(connectionId: string, databaseName?: string): Observable<TableSchema[]> {
    const url = databaseName 
      ? `${this.baseUrl}/schema/tables/${connectionId}?databaseName=${encodeURIComponent(databaseName)}`
      : `${this.baseUrl}/schema/tables/${connectionId}`;
    return this.http.get<TableSchema[]>(url);
  }

  getColumns(connectionId: string, databaseName: string, tableName: string): Observable<ColumnSchema[]> {
    return this.http.get<ColumnSchema[]>(`${this.baseUrl}/schema/columns/${connectionId}?databaseName=${encodeURIComponent(databaseName)}&tableName=${encodeURIComponent(tableName)}`);
  }

  getStoredProcedures(connectionId: string, databaseName: string): Observable<StoredProcedure[]> {
    return this.http.get<StoredProcedure[]>(`${this.baseUrl}/schema/stored-procedures/${connectionId}?databaseName=${encodeURIComponent(databaseName)}`);
  }

  getRelations(connectionId: string, databaseName: string): Observable<TableRelation[]> {
    return this.http.get<TableRelation[]>(`${this.baseUrl}/schema/relations/${connectionId}?databaseName=${encodeURIComponent(databaseName)}`);
  }

  // Query API
  executeQuery(request: QueryRequest): Observable<QueryResult> {
    return this.http.post<QueryResult>(`${this.baseUrl}/query/execute`, request);
  }

  // Data Grid API
  getTableData(request: DataPageRequest): Observable<DataPageResult> {
    return this.http.post<DataPageResult>(`${this.baseUrl}/data/page`, request);
  }

  insertRow(request: RowMutationRequest): Observable<boolean> {
    return this.http.post<boolean>(`${this.baseUrl}/data/insert`, request);
  }

  updateRow(request: RowMutationRequest): Observable<boolean> {
    return this.http.put<boolean>(`${this.baseUrl}/data/update`, request);
  }

  deleteRow(request: RowMutationRequest): Observable<boolean> {
    return this.http.post<boolean>(`${this.baseUrl}/data/delete`, request);
  }

  // Audit Logs API
  getAuditLogs(): Observable<AuditLog[]> {
    return this.http.get<AuditLog[]>(`${this.baseUrl}/audit`);
  }

  // Enterprise Auth & RBAC API
  login(request: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/auth/login`, request);
  }

  getTenants(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/auth/tenants`);
  }

  createTenant(request: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/auth/tenants`, request);
  }

  getTenantUsers(tenantId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/auth/tenants/${tenantId}/users`);
  }

  createUser(request: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/auth/users`, request);
  }

  // Import / Export API
  exportTable(request: ExportTableRequest): Observable<ExportResult> {
    return this.http.post<ExportResult>(`${this.baseUrl}/importexport/export/table`, request);
  }

  downloadExportTable(request: ExportTableRequest): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/importexport/export/table/download`, request, { responseType: 'blob' });
  }

  exportDatabase(request: ExportDatabaseRequest): Observable<ExportResult> {
    return this.http.post<ExportResult>(`${this.baseUrl}/importexport/export/database`, request);
  }

  downloadExportDatabase(request: ExportDatabaseRequest): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/importexport/export/database/download`, request, { responseType: 'blob' });
  }

  importTable(request: ImportTableRequest): Observable<ImportResult> {
    return this.http.post<ImportResult>(`${this.baseUrl}/importexport/import/table`, request);
  }

  importScript(request: ImportDatabaseRequest): Observable<ImportResult> {
    return this.http.post<ImportResult>(`${this.baseUrl}/importexport/import/script`, request);
  }

  // BI Reporting API
  getTelemetryMetrics(connectionId?: string): Observable<TelemetryMetrics> {
    const params = connectionId ? `?connectionId=${connectionId}` : '';
    return this.http.get<TelemetryMetrics>(`${this.baseUrl}/reporting/telemetry${params}`);
  }

  getDashboardAnalytics(connectionId?: string): Observable<DashboardAnalytics> {
    const params = connectionId ? `?connectionId=${connectionId}` : '';
    return this.http.get<DashboardAnalytics>(`${this.baseUrl}/reporting/analytics${params}`);
  }

  getSavedReports(): Observable<Report[]> {
    return this.http.get<Report[]>(`${this.baseUrl}/reporting/reports`);
  }

  createReport(request: CreateReportRequest): Observable<Report> {
    return this.http.post<Report>(`${this.baseUrl}/reporting/reports`, request);
  }

  updateReport(id: string, request: CreateReportRequest): Observable<Report> {
    return this.http.put<Report>(`${this.baseUrl}/reporting/reports/${id}`, request);
  }

  deleteReport(id: string): Observable<any> {
    return this.http.delete<any>(`${this.baseUrl}/reporting/reports/${id}`);
  }

  executeReportQuery(reportId: string, connectionId: string, databaseName: string): Observable<QueryResult> {
    return this.http.post<QueryResult>(`${this.baseUrl}/reporting/reports/${reportId}/execute?connectionId=${connectionId}&databaseName=${databaseName}`, {});
  }

  // Database Backup & Restore API
  createBackup(request: CreateBackupRequest): Observable<BackupResult> {
    return this.http.post<BackupResult>(`${this.baseUrl}/backuprestore/create`, request);
  }

  downloadBackup(id: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/backuprestore/download/${id}`, { responseType: 'blob' });
  }

  restoreDatabase(request: RestoreDatabaseRequest): Observable<RestoreResult> {
    return this.http.post<RestoreResult>(`${this.baseUrl}/backuprestore/restore`, request);
  }

  getBackupHistory(connectionId: string): Observable<DatabaseBackup[]> {
    return this.http.get<DatabaseBackup[]>(`${this.baseUrl}/backuprestore/history/${connectionId}`);
  }
}
