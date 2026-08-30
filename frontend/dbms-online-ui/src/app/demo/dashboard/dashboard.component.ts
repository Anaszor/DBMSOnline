import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SharedModule } from 'src/app/theme/shared/shared.module';
import { ApiService } from 'src/app/services/api.service';
import {
  ConnectionRequest,
  ConnectionResponse,
  DatabaseType,
  AuthType,
  TableSchema,
  ColumnSchema,
  QueryResult,
  DataPageResult,
  AuditLog,
  QueryRiskLevel
} from 'src/app/models/dbms.models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, SharedModule],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit {
  // Enum references for Template
  DatabaseType = DatabaseType;
  AuthType = AuthType;
  QueryRiskLevel = QueryRiskLevel;

  // Connections State
  connections: ConnectionResponse[] = [];
  selectedConnection: ConnectionResponse | null = null;
  showConnectionModal = false;
  connectionTestStatus: { success?: boolean; message?: string } | null = null;
  apiConnected = false;

  // Connection Form State
  connForm: ConnectionRequest = {
    name: '',
    databaseType: DatabaseType.Sqlite,
    authType: AuthType.BasicPassword,
    host: 'localhost',
    port: 1433,
    databaseName: '',
    username: '',
    password: '',
    customConnectionString: '',
    isReadOnly: false,
    enforceSsl: true
  };

  // Database & Table Explorer State
  databases: string[] = [];
  selectedDatabase = '';
  tables: TableSchema[] = [];
  selectedTable = '';
  columns: ColumnSchema[] = [];

  // Active Tab: 'query' | 'data' | 'audit'
  activeTab: 'query' | 'data' | 'audit' = 'query';

  // Query Editor State
  queryText = 'SELECT * FROM Employees;';
  queryResult: QueryResult | null = null;
  isExecutingQuery = false;
  maxRows = 1000;

  // Data Page Grid State
  dataPageResult: DataPageResult | null = null;
  currentPage = 1;
  pageSize = 25;
  isLoadingData = false;

  // Audit Logs State
  auditLogs: AuditLog[] = [];

  // Row Mutation Modal State
  showRowModal = false;
  rowModalMode: 'insert' | 'update' = 'insert';
  rowFormData: Record<string, any> = {};
  rowPrimaryKey: Record<string, any> = {};

  constructor(private apiService: ApiService) {}

  ngOnInit(): void {
    this.loadConnections();
  }

  // Safe Getters for Template Iteration to prevent NG02200 null differ errors
  get queryResultColumns(): string[] {
    return this.queryResult?.columns || [];
  }

  get queryResultRows(): Record<string, any>[] {
    return this.queryResult?.rows || [];
  }

  get dataPageColumns(): ColumnSchema[] {
    return this.dataPageResult?.columns || [];
  }

  get dataPageRows(): Record<string, any>[] {
    return this.dataPageResult?.rows || [];
  }

  isSqlite(type: any): boolean {
    return Number(type) === DatabaseType.Sqlite;
  }

  isBasicAuth(type: any): boolean {
    return Number(type) === AuthType.BasicPassword;
  }

  loadConnections(): void {
    this.apiService.getConnections().subscribe({
      next: (conns) => {
        this.apiConnected = true;
        this.connections = conns;
        if (conns.length > 0) {
          this.selectConnection(conns[0]);
        } else {
          this.addDefaultDemoConnection();
        }
      },
      error: (err) => {
        this.apiConnected = false;
        console.warn('API backend not reachable at http://localhost:5123/api:', err);
        this.addDefaultDemoConnection();
      }
    });
  }

  addDefaultDemoConnection(): void {
    const demo: ConnectionResponse = {
      id: 'demo-local-sqlite',
      name: 'Local Demo SQLite DB',
      databaseType: DatabaseType.Sqlite,
      authType: AuthType.BasicPassword,
      host: 'localhost',
      port: 0,
      databaseName: 'demo_dbms.db',
      username: 'admin',
      isReadOnly: false,
      enforceSsl: false,
      createdAt: new Date().toISOString()
    };
    this.connections = [demo];
    this.selectedConnection = demo;
    this.databases = ['main.db'];
    this.selectedDatabase = 'main.db';
    this.tables = [
      { tableName: 'Employees', schemaName: 'main', tableType: 'TABLE', approxRowCount: 3 },
      { tableName: 'Departments', schemaName: 'main', tableType: 'TABLE', approxRowCount: 3 }
    ];
    this.selectedTable = 'Employees';
    this.columns = [
      { columnName: 'Id', dataType: 'INTEGER', isNullable: false, isPrimaryKey: true, isAutoIncrement: true, defaultValue: '', maxLength: 0 },
      { columnName: 'FullName', dataType: 'TEXT', isNullable: false, isPrimaryKey: false, isAutoIncrement: false, defaultValue: '', maxLength: 0 },
      { columnName: 'Position', dataType: 'TEXT', isNullable: true, isPrimaryKey: false, isAutoIncrement: false, defaultValue: '', maxLength: 0 },
      { columnName: 'Department', dataType: 'TEXT', isNullable: true, isPrimaryKey: false, isAutoIncrement: false, defaultValue: '', maxLength: 0 },
      { columnName: 'Salary', dataType: 'REAL', isNullable: true, isPrimaryKey: false, isAutoIncrement: false, defaultValue: '', maxLength: 0 },
      { columnName: 'Email', dataType: 'TEXT', isNullable: true, isPrimaryKey: false, isAutoIncrement: false, defaultValue: '', maxLength: 0 },
      { columnName: 'Password', dataType: 'TEXT', isNullable: true, isPrimaryKey: false, isAutoIncrement: false, defaultValue: '', maxLength: 0 }
    ];
  }

  selectConnection(conn: ConnectionResponse): void {
    this.selectedConnection = conn;
    if (this.apiConnected) {
      this.loadDatabases(conn.id);
    }
  }

  loadDatabases(connId: string): void {
    this.apiService.getDatabases(connId).subscribe({
      next: (res) => {
        this.databases = res.databases;
        this.selectedDatabase = res.activeDatabase || (res.databases.length > 0 ? res.databases[0] : '');
        if (this.selectedDatabase) {
          this.loadTables();
        }
      },
      error: (err) => console.error('Failed to load databases', err)
    });
  }

  onDatabaseChange(): void {
    if (this.selectedConnection && this.apiConnected) {
      this.loadTables();
    }
  }

  loadTables(): void {
    if (!this.selectedConnection) return;
    this.apiService.getTables(this.selectedConnection.id, this.selectedDatabase).subscribe({
      next: (tables) => {
        this.tables = tables;
        if (tables.length > 0) {
          this.selectTable(tables[0].tableName);
        } else {
          this.selectedTable = '';
          this.columns = [];
        }
      },
      error: (err) => console.error('Failed to load tables', err)
    });
  }

  selectTable(tableName: string): void {
    this.selectedTable = tableName;
    if (!this.selectedConnection) return;
    if (this.apiConnected) {
      this.apiService.getColumns(this.selectedConnection.id, this.selectedDatabase, tableName).subscribe({
        next: (cols) => {
          this.columns = cols;
          if (this.activeTab === 'data') {
            this.loadTableData();
          }
        }
      });
    }
  }

  // --- Connection Modal Actions ---
  openNewConnectionModal(): void {
    this.connForm = {
      name: 'New DB Connection',
      databaseType: DatabaseType.Sqlite,
      authType: AuthType.BasicPassword,
      host: 'localhost',
      port: 1433,
      databaseName: '',
      username: '',
      password: '',
      customConnectionString: '',
      isReadOnly: false,
      enforceSsl: true
    };
    this.connectionTestStatus = null;
    this.showConnectionModal = true;
  }

  onDbTypeChange(): void {
    const type = Number(this.connForm.databaseType);
    if (type === DatabaseType.SqlServer) this.connForm.port = 1433;
    else if (type === DatabaseType.PostgreSql) this.connForm.port = 5432;
    else if (type === DatabaseType.MySql) this.connForm.port = 3306;
    else if (type === DatabaseType.MongoDb) this.connForm.port = 27017;
  }

  testConnection(): void {
    this.connectionTestStatus = { message: 'Testing connection...' };
    this.apiService.testConnection(this.connForm).subscribe({
      next: (res) => this.connectionTestStatus = res,
      error: (err) => this.connectionTestStatus = { success: false, message: err.error?.detail || err.message }
    });
  }

  saveConnection(): void {
    this.apiService.saveConnection(this.connForm).subscribe({
      next: (res) => {
        this.showConnectionModal = false;
        this.loadConnections();
        this.selectConnection(res);
      },
      error: (err) => alert('Error saving connection: ' + (err.error?.detail || err.message))
    });
  }

  deleteConnection(id: string, event: Event): void {
    event.stopPropagation();
    if (!confirm('Are you sure you want to delete this saved connection profile?')) return;
    this.apiService.deleteConnection(id).subscribe({
      next: () => {
        if (this.selectedConnection?.id === id) this.selectedConnection = null;
        this.loadConnections();
      }
    });
  }

  // --- Query Execution ---
  runQuery(): void {
    if (!this.selectedConnection || !this.queryText.trim()) return;
    this.isExecutingQuery = true;
    this.queryResult = null;

    this.apiService.executeQuery({
      connectionId: this.selectedConnection.id,
      databaseName: this.selectedDatabase,
      queryText: this.queryText,
      maxRows: this.maxRows,
      timeoutSeconds: 30
    }).subscribe({
      next: (res) => {
        this.queryResult = res;
        this.isExecutingQuery = false;
      },
      error: (err) => {
        this.isExecutingQuery = false;
        this.queryResult = {
          columns: [],
          rows: [],
          rowsAffected: 0,
          executionDurationMs: 0,
          riskLevel: QueryRiskLevel.Critical,
          warningMessage: err.error?.title || 'Execution Error',
          isTruncated: false,
          success: false,
          errorMessage: err.error?.detail || err.message || 'Failed to execute query. Ensure ASP.NET Core API backend is running on http://localhost:5123'
        };
      }
    });
  }

  // --- Data View Page ---
  switchTab(tab: 'query' | 'data' | 'audit'): void {
    this.activeTab = tab;
    if (tab === 'data' && this.selectedTable) {
      this.loadTableData();
    } else if (tab === 'audit') {
      this.loadAuditLogs();
    }
  }

  loadTableData(): void {
    if (!this.selectedConnection || !this.selectedTable) return;
    this.isLoadingData = true;

    this.apiService.getTableData({
      connectionId: this.selectedConnection.id,
      databaseName: this.selectedDatabase,
      tableName: this.selectedTable,
      page: this.currentPage,
      pageSize: this.pageSize
    }).subscribe({
      next: (res) => {
        this.dataPageResult = res;
        this.isLoadingData = false;
      },
      error: (err) => {
        this.isLoadingData = false;
        alert('Failed to load table data: ' + (err.error?.detail || err.message));
      }
    });
  }

  changePage(newPage: number): void {
    if (newPage >= 1 && (!this.dataPageResult || newPage <= this.dataPageResult.totalPages)) {
      this.currentPage = newPage;
      this.loadTableData();
    }
  }

  // --- Row CRUD Operations ---
  openAddRowModal(): void {
    this.rowModalMode = 'insert';
    this.rowFormData = {};
    this.columns.forEach(col => this.rowFormData[col.columnName] = '');
    this.showRowModal = true;
  }

  openEditRowModal(row: Record<string, any>): void {
    this.rowModalMode = 'update';
    this.rowFormData = { ...row };
    this.rowPrimaryKey = {};

    const pkCols = this.columns.filter(c => c.isPrimaryKey);
    if (pkCols.length > 0) {
      pkCols.forEach(c => this.rowPrimaryKey[c.columnName] = row[c.columnName]);
    } else {
      const firstCol = this.columns[0]?.columnName || 'id';
      this.rowPrimaryKey[firstCol] = row[firstCol];
    }

    this.showRowModal = true;
  }

  saveRowMutation(): void {
    if (!this.selectedConnection || !this.selectedTable) return;

    if (this.rowModalMode === 'insert') {
      this.apiService.insertRow({
        connectionId: this.selectedConnection.id,
        databaseName: this.selectedDatabase,
        tableName: this.selectedTable,
        primaryKey: {},
        data: this.rowFormData
      }).subscribe({
        next: () => {
          this.showRowModal = false;
          this.loadTableData();
        },
        error: (err) => alert('Insert failed: ' + (err.error?.detail || err.message))
      });
    } else {
      this.apiService.updateRow({
        connectionId: this.selectedConnection.id,
        databaseName: this.selectedDatabase,
        tableName: this.selectedTable,
        primaryKey: this.rowPrimaryKey,
        data: this.rowFormData
      }).subscribe({
        next: () => {
          this.showRowModal = false;
          this.loadTableData();
        },
        error: (err) => alert('Update failed: ' + (err.error?.detail || err.message))
      });
    }
  }

  deleteRow(row: Record<string, any>): void {
    if (!confirm('Are you sure you want to delete this row?')) return;
    if (!this.selectedConnection || !this.selectedTable) return;

    const pk: Record<string, any> = {};
    const pkCols = this.columns.filter(c => c.isPrimaryKey);
    if (pkCols.length > 0) {
      pkCols.forEach(c => pk[c.columnName] = row[c.columnName]);
    } else {
      const firstCol = this.columns[0]?.columnName || 'id';
      pk[firstCol] = row[firstCol];
    }

    this.apiService.deleteRow({
      connectionId: this.selectedConnection.id,
      databaseName: this.selectedDatabase,
      tableName: this.selectedTable,
      primaryKey: pk,
      data: {}
    }).subscribe({
      next: () => this.loadTableData(),
      error: (err) => alert('Delete failed: ' + (err.error?.detail || err.message))
    });
  }

  // --- Audit Logs ---
  loadAuditLogs(): void {
    this.apiService.getAuditLogs().subscribe({
      next: (logs) => this.auditLogs = logs
    });
  }

  // --- Export Helpers ---
  exportToJson(): void {
    if (!this.queryResult?.rows) return;
    const jsonStr = JSON.stringify(this.queryResult.rows, null, 2);
    this.downloadFile(jsonStr, 'query_export.json', 'application/json');
  }

  exportToCsv(): void {
    if (!this.queryResult?.rows || !this.queryResult.columns) return;
    const cols = this.queryResult.columns;
    const csvRows = [cols.join(',')];

    for (const row of this.queryResult.rows) {
      const vals = cols.map(c => {
        const val = row[c] ?? '';
        return `"${String(val).replace(/"/g, '""')}"`;
      });
      csvRows.push(vals.join(','));
    }

    this.downloadFile(csvRows.join('\n'), 'query_export.csv', 'text/csv');
  }

  private downloadFile(content: string, filename: string, mime: string): void {
    const blob = new Blob([content], { type: mime });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }

  getDbTypeName(type: DatabaseType): string {
    const t = Number(type);
    switch (t) {
      case DatabaseType.SqlServer: return 'MS SQL';
      case DatabaseType.PostgreSql: return 'PostgreSQL';
      case DatabaseType.MySql: return 'MySQL';
      case DatabaseType.Sqlite: return 'SQLite';
      case DatabaseType.MongoDb: return 'MongoDB';
      default: return 'SQL';
    }
  }

  getDbTypeTagClass(type: DatabaseType): string {
    const t = Number(type);
    switch (t) {
      case DatabaseType.SqlServer: return 'badge bg-danger';
      case DatabaseType.PostgreSql: return 'badge bg-primary';
      case DatabaseType.MySql: return 'badge bg-warning text-dark';
      case DatabaseType.Sqlite: return 'badge bg-info';
      case DatabaseType.MongoDb: return 'badge bg-success';
      default: return 'badge bg-secondary';
    }
  }
}
