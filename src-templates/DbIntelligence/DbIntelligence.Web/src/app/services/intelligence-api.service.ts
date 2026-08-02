import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface GraphifyGraph {
  nodes: GraphNode[];
  edges: GraphEdge[];
  meta?: { generatedAt?: string; sources?: string[]; targetRepositoryPath?: string };
}

export interface GraphNode {
  id: string;
  label: string;
  kind?: string;
  source_file?: string;
  source_location?: string;
  community?: string;
  schema?: string;
  database?: string;
}

export interface GraphEdge {
  source: string;
  target: string;
  relation: string;
  confidence: string;
  evidence?: { file?: string; line?: number; pattern?: string; raw_reference?: string };
}

export interface IndexJob {
  id: string;
  status: string;
  phase?: string;
  message?: string;
  log?: string[];
}

export interface BatchProjectResult {
  name: string;
  path: string;
  status: string;
  message?: string;
  artifactsDirectory?: string;
  nodeCount?: number;
  edgeCount?: number;
}

export interface BatchIndexJob {
  id: string;
  status: string;
  phase?: string;
  message?: string;
  parentFolderPath: string;
  totalProjects: number;
  completedProjects: number;
  failedProjects: number;
  currentProject?: string;
  projects: BatchProjectResult[];
  log?: string[];
}

export interface DiscoveredProjects {
  parentFolderPath: string;
  projects: Array<{ name: string; path: string; hasProjectMarker: boolean }>;
}

export interface CodeToDbMap {
  entries: Array<{
    codeNodeId: string;
    codeLabel: string;
    sourceFile?: string;
    line?: number;
    dbNodeId: string;
    dbObject: string;
    dbKind: string;
    relation: string;
    confidence: string;
    pattern?: string;
    project?: string;
  }>;
}

export interface CombineGraphsResult {
  parentFolderPath: string;
  projectsLoaded: number;
  projectsSkipped: number;
  nodeCount: number;
  edgeCount: number;
  combinedOutputDirectory?: string;
  loaded: Array<{
    name: string;
    path: string;
    status: string;
    message?: string;
    nodeCount?: number;
    edgeCount?: number;
  }>;
  skipped: Array<{
    name: string;
    path: string;
    status: string;
    message?: string;
  }>;
}

export interface StoredProcedureMap {
  procedures: Array<{
    id: string;
    name: string;
    schema?: string;
    database?: string;
    codeCallers: string[];
    sqlCallers: string[];
    reads: string[];
    writes: string[];
  }>;
}

@Injectable({ providedIn: 'root' })
export class IntelligenceApiService {
  private readonly base = '/api';

  constructor(private readonly http: HttpClient) {}

  getUnifiedGraph(opts: { kind?: string; confidence?: string; codeToDbOnly?: boolean } = {}): Observable<GraphifyGraph> {
    let params = new HttpParams();
    if (opts.kind) params = params.set('kind', opts.kind);
    if (opts.confidence) params = params.set('confidence', opts.confidence);
    if (opts.codeToDbOnly) params = params.set('codeToDbOnly', 'true');
    return this.http.get<GraphifyGraph>(`${this.base}/graphs/unified`, { params });
  }

  explore(q: string, depth = 1): Observable<GraphifyGraph> {
    const params = new HttpParams().set('q', q).set('depth', String(depth));
    return this.http.get<GraphifyGraph>(`${this.base}/explore`, { params });
  }

  search(q: string) {
    return this.http.get<Array<{ id: string; label: string; kind: string }>>(`${this.base}/search`, {
      params: new HttpParams().set('q', q)
    });
  }

  getNode(id: string) {
    return this.http.get<{ node: GraphNode; callers: unknown[]; callees: unknown[] }>(
      `${this.base}/nodes/${encodeURIComponent(id)}`
    );
  }

  getTools() {
    return this.http.get<{
      codegraphAvailable: boolean;
      graphifyAvailable: boolean;
      pythonAvailable: boolean;
      pipAvailable: boolean;
      healthy: boolean;
      codegraphExecutable: string;
      graphifyExecutable: string;
      message?: string;
      prerequisites?: {
        status: string;
        missing: string[];
        installHint: string;
        python: { available: boolean; versionOrDetail?: string };
        graphify: { available: boolean; versionOrDetail?: string };
        codegraph: { available: boolean; versionOrDetail?: string };
      };
    }>(`${this.base}/tools`);
  }

  startIndex(body: {
    targetRepositoryPath: string;
    runCodegraph?: boolean;
    runGraphify?: boolean;
    runRepositoryScan?: boolean;
    runSqlScan?: boolean;
    artifactsRelativeDirectory?: string;
  }) {
    return this.http.post<IndexJob>(`${this.base}/index/jobs`, body);
  }

  discoverProjects(parentFolderPath: string, requireProjectMarkers = false) {
    let params = new HttpParams().set('parentFolderPath', parentFolderPath);
    if (requireProjectMarkers) params = params.set('requireProjectMarkers', 'true');
    return this.http.get<DiscoveredProjects>(`${this.base}/index/discover`, { params });
  }

  startBatchIndex(body: {
    parentFolderPath: string;
    runCodegraph?: boolean;
    runGraphify?: boolean;
    refreshGraphify?: boolean;
    runRepositoryScan?: boolean;
    runSqlScan?: boolean;
    requireProjectMarkers?: boolean;
    continueOnError?: boolean;
    artifactsRelativeDirectory?: string;
  }) {
    return this.http.post<BatchIndexJob>(`${this.base}/index/batch`, body);
  }

  getBatchJob(id: string) {
    return this.http.get<BatchIndexJob>(`${this.base}/index/batch/${id}`);
  }

  getJob(id: string) {
    return this.http.get<IndexJob>(`${this.base}/index/jobs/${id}`);
  }

  getCodeToDbMap() {
    return this.http.get<CodeToDbMap>(`${this.base}/maps/code-to-db`);
  }

  getStoredProcedureMap() {
    return this.http.get<StoredProcedureMap>(`${this.base}/maps/stored-procedures`);
  }

  export() {
    return this.http.post<{ outputDirectory: string }>(`${this.base}/export`, {});
  }

  combineGraphs(body: {
    parentFolderPath: string;
    artifactsRelativeDirectory?: string;
    requireProjectMarkers?: boolean;
    shareDatabaseNodes?: boolean;
    onlyCompletedFromSummary?: boolean;
    exportCombined?: boolean;
  }) {
    return this.http.post<CombineGraphsResult>(`${this.base}/graphs/combine`, body);
  }
}
