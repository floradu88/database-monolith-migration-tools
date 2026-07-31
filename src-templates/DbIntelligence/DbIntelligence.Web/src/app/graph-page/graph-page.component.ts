import {
  AfterViewInit,
  Component,
  ElementRef,
  OnDestroy,
  ViewChild,
  inject
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DataSet } from 'vis-data';
import { Network, Options } from 'vis-network';
import {
  CodeToDbMap,
  GraphNode,
  GraphifyGraph,
  IndexJob,
  BatchIndexJob,
  IntelligenceApiService,
  StoredProcedureMap
} from '../services/intelligence-api.service';

type ViewMode = 'graph' | 'code-to-db' | 'procedures';
type IndexMode = 'single' | 'batch';

@Component({
  selector: 'app-graph-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './graph-page.component.html',
  styleUrl: './graph-page.component.css'
})
export class GraphPageComponent implements AfterViewInit, OnDestroy {
  @ViewChild('networkHost', { static: true }) networkHost!: ElementRef<HTMLDivElement>;

  private readonly api = inject(IntelligenceApiService);
  private network?: Network;
  private nodes = new DataSet<any>([]);
  private edges = new DataSet<any>([]);

  query = '';
  kindFilter = '';
  confidenceFilter = '';
  codeToDbOnly = false;
  view: ViewMode = 'graph';
  selected?: GraphNode;
  selectedCallers: unknown[] = [];
  selectedCallees: unknown[] = [];
  statusMessage = 'Load a graph or run an index job.';
  job?: IndexJob;
  batchJob?: BatchIndexJob;
  codeMap?: CodeToDbMap;
  spMap?: StoredProcedureMap;
  repoPath = '';
  parentFolderPath = '';
  indexMode: IndexMode = 'single';
  requireProjectMarkers = false;
  discoveredNames: string[] = [];
  toolStatus = 'Checking CLI tools...';

  private pollHandle?: ReturnType<typeof setInterval>;

  ngAfterViewInit(): void {
    this.checkTools();
    const options: Options = {
      physics: {
        enabled: true,
        barnesHut: { gravitationalConstant: -12000, springLength: 120 }
      },
      nodes: {
        shape: 'dot',
        font: { face: 'IBM Plex Sans', size: 13, color: '#1b1f24' },
        borderWidth: 1
      },
      edges: {
        arrows: { to: { enabled: true, scaleFactor: 0.6 } },
        font: { align: 'middle', size: 10, face: 'IBM Plex Sans' },
        smooth: { type: 'continuous', enabled: true, roundness: 0.4 }
      },
      interaction: { hover: true, tooltipDelay: 120 }
    };

    this.network = new Network(
      this.networkHost.nativeElement,
      { nodes: this.nodes, edges: this.edges },
      options
    );

    this.network.on('click', (params) => {
      if (params.nodes?.length) {
        this.selectNode(String(params.nodes[0]));
      }
    });

    this.reloadGraph();
  }

  ngOnDestroy(): void {
    if (this.pollHandle) clearInterval(this.pollHandle);
    this.network?.destroy();
  }

  reloadGraph(): void {
    this.statusMessage = 'Loading graph...';
    const req$ = this.query.trim()
      ? this.api.explore(this.query.trim(), 2)
      : this.api.getUnifiedGraph({
          kind: this.kindFilter || undefined,
          confidence: this.confidenceFilter || undefined,
          codeToDbOnly: this.codeToDbOnly
        });

    req$.subscribe({
      next: (graph) => {
        this.renderGraph(graph);
        this.statusMessage = `${graph.nodes.length} nodes · ${graph.edges.length} edges`;
      },
      error: (err) => {
        this.statusMessage = `Failed to load graph: ${err.message || err.statusText || 'error'}`;
      }
    });
  }

  loadMaps(): void {
    this.api.getCodeToDbMap().subscribe((m) => (this.codeMap = m));
    this.api.getStoredProcedureMap().subscribe((m) => (this.spMap = m));
  }

  setView(view: ViewMode): void {
    this.view = view;
    if (view !== 'graph') this.loadMaps();
  }

  startIndex(): void {
    if (!this.repoPath.trim()) {
      this.statusMessage = 'Enter a repository folder path to index.';
      return;
    }

    this.api
      .startIndex({
        targetRepositoryPath: this.repoPath.trim(),
        runCodegraph: true,
        runGraphify: true,
        runRepositoryScan: true,
        runSqlScan: false
      })
      .subscribe({
        next: (job) => {
          this.job = job;
          this.batchJob = undefined;
          this.statusMessage = `Index job ${job.id} started for ${this.repoPath.trim()}`;
          this.pollJob(job.id);
        },
        error: (err) =>
          (this.statusMessage = `Index failed: ${err.error?.message || err.message || err.status}`)
      });
  }

  discoverProjects(): void {
    if (!this.parentFolderPath.trim()) {
      this.statusMessage = 'Enter a parent folder that contains project subfolders.';
      return;
    }

    this.api.discoverProjects(this.parentFolderPath.trim(), this.requireProjectMarkers).subscribe({
      next: (d) => {
        this.discoveredNames = d.projects.map((p) => p.name);
        this.statusMessage = `Found ${d.projects.length} project(s) under ${d.parentFolderPath}`;
      },
      error: (err) =>
        (this.statusMessage = `Discover failed: ${err.error?.message || err.message || err.status}`)
    });
  }

  startBatchIndex(): void {
    if (!this.parentFolderPath.trim()) {
      this.statusMessage = 'Enter a parent folder that contains project subfolders.';
      return;
    }

    this.api
      .startBatchIndex({
        parentFolderPath: this.parentFolderPath.trim(),
        runCodegraph: true,
        runGraphify: true,
        runRepositoryScan: true,
        runSqlScan: false,
        requireProjectMarkers: this.requireProjectMarkers,
        continueOnError: true,
        artifactsRelativeDirectory: ''
      })
      .subscribe({
        next: (job) => {
          this.batchJob = job;
          this.job = undefined;
          this.statusMessage = `Batch ${job.id}: ${job.totalProjects} projects`;
          this.pollBatchJob(job.id);
        },
        error: (err) =>
          (this.statusMessage = `Batch failed: ${err.error?.message || err.message || err.status}`)
      });
  }

  checkTools(): void {
    this.api.getTools().subscribe({
      next: (tools) => {
        const missing = tools.prerequisites?.missing?.length
          ? ` Missing: ${tools.prerequisites.missing.join(', ')}.`
          : '';
        const hint = tools.healthy
          ? ''
          : ' Run: dotnet run --project src-templates/DbIntelligence/DbIntelligence.Cli -- --install-preqs';
        this.toolStatus = `${tools.message || ''}${missing}${hint}`;
        this.statusMessage = this.toolStatus;
      },
      error: (err) => (this.statusMessage = `Tool check failed: ${err.message || err.status}`)
    });
  }

  exportArtifacts(): void {
    this.api.export().subscribe({
      next: (r) => (this.statusMessage = `Exported to ${r.outputDirectory}`),
      error: (err) => (this.statusMessage = `Export failed: ${err.message || err.status}`)
    });
  }

  private pollJob(id: string): void {
    if (this.pollHandle) clearInterval(this.pollHandle);
    this.pollHandle = setInterval(() => {
      this.api.getJob(id).subscribe((job) => {
        this.job = job;
        this.statusMessage = `${job.status}: ${job.phase || ''} ${job.message || ''}`;
        if (job.status === 'Completed' || job.status === 'Failed') {
          if (this.pollHandle) clearInterval(this.pollHandle);
          if (job.status === 'Completed') this.reloadGraph();
        }
      });
    }, 1500);
  }

  private pollBatchJob(id: string): void {
    if (this.pollHandle) clearInterval(this.pollHandle);
    this.pollHandle = setInterval(() => {
      this.api.getBatchJob(id).subscribe((job) => {
        this.batchJob = job;
        this.statusMessage = `${job.status}: ${job.completedProjects}/${job.totalProjects} ${job.currentProject || ''} ${job.message || ''}`;
        if (job.status === 'Completed' || job.status === 'Failed') {
          if (this.pollHandle) clearInterval(this.pollHandle);
          if (job.status === 'Completed') this.reloadGraph();
        }
      });
    }, 2000);
  }

  private selectNode(id: string): void {
    this.api.getNode(id).subscribe({
      next: (detail) => {
        this.selected = detail.node;
        this.selectedCallers = detail.callers || [];
        this.selectedCallees = detail.callees || [];
      }
    });
  }

  private renderGraph(graph: GraphifyGraph): void {
    const communities = Array.from(
      new Set(graph.nodes.map((n) => n.community || n.kind || 'default'))
    );
    const colorFor = (key: string) => {
      const palette = ['#2f6fed', '#0f8a5f', '#c45c26', '#6b4fbb', '#b08900', '#0e7490'];
      const idx = Math.abs(hash(key)) % palette.length;
      return palette[idx];
    };

    this.nodes.clear();
    this.edges.clear();
    this.nodes.add(
      graph.nodes.map((n) => ({
        id: n.id,
        label: n.label,
        title: `${n.kind || ''}\n${n.source_file || ''}\n${n.source_location || ''}`,
        color: {
          background: colorFor(n.community || n.kind || 'default'),
          border: '#1b1f24',
          highlight: { background: '#111827', border: '#111827' }
        },
        size: n.id.startsWith('db:') ? 18 : 14
      }))
    );

    this.edges.add(
      graph.edges.map((e, i) => ({
        id: `e-${i}`,
        from: e.source,
        to: e.target,
        label: e.relation,
        dashes: e.confidence === 'INFERRED' ? [6, 4] : e.confidence === 'AMBIGUOUS' ? [2, 4] : false,
        color: { color: e.confidence === 'AMBIGUOUS' ? '#9a3412' : '#64748b' },
        title: `${e.relation} (${e.confidence})`
      }))
    );

    void communities;
  }
}

function hash(value: string): number {
  let h = 0;
  for (let i = 0; i < value.length; i++) h = (h << 5) - h + value.charCodeAt(i);
  return h;
}
