export enum DatabaseType {
  SqlServer = 1,
  PostgreSql = 2,
  MySql = 3,
  Sqlite = 4,
  MongoDb = 5,
  Oracle = 6
}

export enum AuthType {
  BasicPassword = 1,
  WindowsAuth = 2,
  ConnectionString = 3,
  MongoDbScram = 4
}

export enum QueryRiskLevel {
  Safe = 0,
  Low = 1,
  Moderate = 2,
  High = 3,
  Critical = 4
}

export interface ConnectionRequest {
  id?: string;
  name: string;
  databaseType: DatabaseType;
  authType: AuthType;
  host: string;
  port: number;
  databaseName: string;
  username: string;
  password?: string;
  customConnectionString?: string;
  isReadOnly: boolean;
  enforceSsl: boolean;
}

export interface ConnectionResponse {
  id: string;
  name: string;
  databaseType: DatabaseType;
  authType: AuthType;
  host: string;
  port: number;
  databaseName: string;
  username: string;
  customConnectionString?: string;
  isReadOnly: boolean;
  enforceSsl: boolean;
  createdAt: string;
}

export interface DatabaseSchema {
  databases: string[];
  activeDatabase: string;
}

export interface ProcedureParameter {
  parameterName: string;
  dataType: string;
  parameterMode: string;
}

export interface StoredProcedure {
  procedureName: string;
  schemaName: string;
  routineType: string; // PROCEDURE or FUNCTION
  definition: string;
  parameters?: ProcedureParameter[];
}

export interface TableRelation {
  pkTable: string;
  pkColumn: string;
  fkTable: string;
  fkColumn: string;
  constraintName: string;
  cardinality?: '1:1' | '1:N' | 'N:M';
}

export interface TableSchema {
  tableName: string;
  schemaName: string;
  tableType: string;
  approxRowCount: number;
}

export interface ColumnSchema {
  columnName: string;
  dataType: string;
  isNullable: boolean;
  isPrimaryKey: boolean;
  isForeignKey?: boolean;
  isAutoIncrement: boolean;
  defaultValue: string;
  maxLength: number;
}

export interface QueryRequest {
  connectionId: string;
  databaseName: string;
  queryText: string;
  maxRows: number;
  timeoutSeconds: number;
}

export interface QueryResult {
  columns: string[];
  rows: Record<string, any>[];
  rowsAffected: number;
  executionDurationMs: number;
  riskLevel: QueryRiskLevel;
  warningMessage: string;
  isTruncated: boolean;
  success: boolean;
  errorMessage: string;
}

export interface DataPageRequest {
  connectionId: string;
  databaseName: string;
  tableName: string;
  page: number;
  pageSize: number;
  sortColumn?: string;
  sortAscending?: boolean;
  filterColumn?: string;
  filterValue?: string;
}

export interface DataPageResult {
  columns: ColumnSchema[];
  rows: Record<string, any>[];
  totalRows: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface RowMutationRequest {
  connectionId: string;
  databaseName: string;
  tableName: string;
  primaryKey: Record<string, any>;
  data: Record<string, any>;
}

export interface AuditLog {
  id: string;
  timestamp: string;
  clientIp: string;
  userRole: string;
  action: string;
  connectionName: string;
  databaseType: DatabaseType;
  targetDatabase: string;
  queryText: string;
  queryHash: string;
  executionDurationMs: number;
  rowsAffected: number;
  riskLevel: QueryRiskLevel;
  success: boolean;
  errorMessage: string;
}

export enum UserRole {
  OwnerSuperAdmin = 1,
  OrgAdmin = 2,
  DBA = 3,
  Developer = 4,
  Auditor = 5
}

export interface User {
  id: string;
  tenantId: string;
  email: string;
  fullName: string;
  role: UserRole;
  roleName: string;
  isActive: boolean;
  createdAt: string;
}

export interface Tenant {
  id: string;
  name: string;
  domain: string;
  planType: string;
  isActive: boolean;
  userCount: number;
  createdAt: string;
}

export interface QueryTab {
  id: string;
  title: string;
  queryText: string;
  active: boolean;
  lastExecutedAt?: Date;
  lastResult?: QueryResult;
}

export interface SuggestItem {
  text: string;
  type: 'keyword' | 'table' | 'view' | 'column' | 'proc';
  dataType?: string;
  tableName?: string;
  isPk?: boolean;
  isFk?: boolean;
  detail?: string;
}

export interface ExportTableRequest {
  connectionId: string;
  databaseName: string;
  tableName: string;
  format: 'sql' | 'csv' | 'json';
  includeData: boolean;
  includeSchema: boolean;
}

export interface ExportDatabaseRequest {
  connectionId: string;
  databaseName: string;
  format: 'sql' | 'csv' | 'json';
}

export interface ExportResult {
  success: boolean;
  fileName: string;
  contentType: string;
  content: string;
  fileSizeBytes: number;
  message: string;
}

export interface ImportTableRequest {
  connectionId: string;
  databaseName: string;
  tableName: string;
  fileFormat: 'csv' | 'json';
  fileContent: string;
  createTableIfNotExists: boolean;
}

export interface ImportDatabaseRequest {
  connectionId: string;
  databaseName: string;
  sqlScript: string;
}

export interface ImportResult {
  success: boolean;
  rowsImported: number;
  statementsExecuted: number;
  message: string;
  errors: string[];
}

export interface ChartDataPoint {
  category: string;
  value: number;
  group?: string;
}

export interface TelemetryMetrics {
  totalQueriesExecuted: number;
  averageExecutionTimeMs: number;
  querySuccessRatePercentage: number;
  activeConnectionsCount: number;
  totalTablesCount: number;
  latencyTrends: ChartDataPoint[];
  riskLevelDistribution: ChartDataPoint[];
  tableSizesDistribution: ChartDataPoint[];
}

export interface DashboardAnalytics {
  telemetry: TelemetryMetrics;
  savedReports: Report[];
}

export type PowerBiWidgetType =
  // Row 1: Bar & Column
  | 'StackedBarChart'
  | 'StackedColumnChart'
  | 'ClusteredBarChart'
  | 'ClusteredColumnChart'
  | 'PercentStackedBarChart'
  | 'PercentStackedColumnChart'
  // Row 2: Line & Area
  | 'LineChart'
  | 'AreaChart'
  | 'StackedAreaChart'
  | 'LineAndStackedColumn'
  | 'LineAndClusteredColumn'
  | 'RibbonChart'
  // Row 3: Category & Hierarchy
  | 'WaterfallChart'
  | 'FunnelChart'
  | 'ScatterChart'
  | 'PieChart'
  | 'DonutChart'
  | 'Treemap'
  // Row 4: Maps, Gauges & Cards
  | 'BubbleMap'
  | 'FilledMap'
  | 'ShapeMap'
  | 'AzureMap'
  | 'GaugeChart'
  | 'Card'
  // Row 5: Cards, Tables & Slicers
  | 'MultiRowCard'
  | 'KpiVisual'
  | 'Slicer'
  | 'Table'
  | 'Matrix'
  | 'RScript'
  // Row 6: AI & Machine Learning
  | 'PythonScript'
  | 'KeyInfluencers'
  | 'DecompositionTree'
  | 'QAndA'
  | 'SmartNarrative'
  | 'Scorecard'
  // Row 7 & 8: Reports & Custom
  | 'PaginatedReport'
  | 'PowerBiServiceAiVisuals'
  | 'CustomVisuals'
  // Legacy aliases
  | 'BarChart'
  | 'ColumnChart'
  | 'ClusteredBar'
  | 'ClusteredColumn'
  | 'LineColumnCombo'
  | 'Funnel'
  | 'Gauge'
  | 'KpiCard'
  | 'DataTable'
  | 'Map'
  | 'Waterfall';

export interface DateHierarchySelection {
  year: boolean;
  quarter: boolean;
  month: boolean;
  day: boolean;
}

export interface FieldBinding {
  field: string;
  role: 'xAxis' | 'yAxis' | 'secondaryYAxis' | 'legend' | 'smallMultiples' | 'tooltips' | 'drillThrough';
  tableName?: string;
  aggregation?: 'SUM' | 'AVG' | 'COUNT' | 'DISTINCT_COUNT' | 'MIN' | 'MAX' | 'NONE';
  hierarchy?: DateHierarchySelection;
}

export interface VisualFormattingConfig {
  showXAxis?: boolean;
  showYAxis?: boolean;
  showLegend?: boolean;
  legendPosition?: 'Top' | 'Bottom' | 'Left' | 'Right';
  showGridlines?: boolean;
  strokeWidth?: number;
  interpolation?: 'smooth' | 'stepped' | 'linear';
  shadeArea?: boolean;
  showMarkers?: boolean;
  markerShape?: 'circle' | 'square' | 'triangle';
  showDataLabels?: boolean;
  dataLabelPosition?: 'Outside' | 'Inside' | 'Auto';
  seriesColors?: Record<string, string>;
  showTitle?: boolean;
  titleText?: string;
  titleFontSize?: number;
  titleAlignment?: 'left' | 'center' | 'right';
  showBorder?: boolean;
  borderColor?: string;
  borderWidth?: number;
  backgroundColor?: string;
  backgroundOpacity?: number;
  showShadow?: boolean;
  showHeaderIcons?: boolean;
  showTooltips?: boolean;
}

export interface VisualAnalyticsConfig {
  showConstantLine?: boolean;
  constantLineValue?: number;
  constantLineColor?: string;
  showAverageLine?: boolean;
  averageLineColor?: string;
  showMedianLine?: boolean;
  showPercentileLine?: boolean;
  percentileValue?: number;
  showTrendline?: boolean;
  trendlineColor?: string;
  showMinMaxLine?: boolean;
  showErrorBars?: boolean;
  showForecast?: boolean;
  showAnomalies?: boolean;
}

export interface Report {
  id: string;
  title: string;
  description: string;
  queryText: string;
  widgetType: PowerBiWidgetType;
  categoryColumn: string;
  measureColumn: string;
  tableName?: string;
  aggregation?: 'SUM' | 'AVG' | 'COUNT' | 'DISTINCT_COUNT' | 'MIN' | 'MAX' | 'NONE';
  filterNulls?: boolean;
  topN?: number;
  sortBy?: string;
  sortDirection?: 'ASC' | 'DESC';
  valueFormat?: 'number' | 'currency' | 'percent';
  positionX?: number;
  positionY?: number;
  width?: number;
  height?: number;
  fieldBindings?: FieldBinding[];
  formatting?: VisualFormattingConfig;
  analytics?: VisualAnalyticsConfig;
  secondaryColumn?: string;
  legendColumn?: string;
  smallMultiplesColumn?: string;
  tooltipColumn?: string;
  pageId?: string;
  createdAt: string;
}

export interface CreateReportRequest {
  title: string;
  description: string;
  queryText: string;
  widgetType: PowerBiWidgetType;
  categoryColumn: string;
  measureColumn: string;
  secondaryColumn?: string;
  legendColumn?: string;
  smallMultiplesColumn?: string;
  tooltipColumn?: string;
  tableName?: string;
  aggregation?: 'SUM' | 'AVG' | 'COUNT' | 'DISTINCT_COUNT' | 'MIN' | 'MAX' | 'NONE';
  filterNulls?: boolean;
  topN?: number;
  sortBy?: string;
  sortDirection?: 'ASC' | 'DESC';
  valueFormat?: 'number' | 'currency' | 'percent';
  positionX?: number;
  positionY?: number;
  width?: number;
  height?: number;
}

export interface CreateBackupRequest {
  connectionId: string;
  databaseName: string;
  backupName: string;
}

export interface RestoreDatabaseRequest {
  connectionId: string;
  targetDatabaseName: string;
  backupContentOrPath: string;
}

export interface DatabaseBackup {
  id: string;
  connectionId: string;
  backupName: string;
  databaseName: string;
  databaseType: DatabaseType;
  fileSizeBytes: number;
  filePath: string;
  backupContent?: string;
  createdAt: string;
}

export interface BackupResult {
  success: boolean;
  backup?: DatabaseBackup;
  backupContent?: string;
  message: string;
}

export interface RestoreResult {
  success: boolean;
  statementsExecuted: number;
  message: string;
}
