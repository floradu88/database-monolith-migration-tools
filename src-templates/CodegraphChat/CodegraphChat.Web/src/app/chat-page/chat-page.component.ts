import { Component, ElementRef, OnInit, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ChatApiService, ChatEvidenceDto } from '../services/chat-api.service';
import { ChatMarkdownPipe } from './chat-markdown.pipe';

interface UiMessage {
  id: string;
  role: 'user' | 'assistant' | 'system';
  content: string;
  at: Date;
  mode?: string;
  symbol?: string;
  evidence?: ChatEvidenceDto[];
}

interface Suggestion {
  label: string;
  prompt: string;
  mode?: string;
}

const REPO_KEY = 'codegraphChat.repoPath';

@Component({
  selector: 'app-chat-page',
  standalone: true,
  imports: [CommonModule, FormsModule, ChatMarkdownPipe],
  templateUrl: './chat-page.component.html',
  styleUrl: './chat-page.component.css'
})
export class ChatPageComponent implements OnInit {
  @ViewChild('scrollHost') scrollHost?: ElementRef<HTMLDivElement>;
  @ViewChild('composer') composer?: ElementRef<HTMLTextAreaElement>;

  private readonly api = inject(ChatApiService);
  private seq = 0;

  repoPath = '';
  draft = '';
  mode = '';
  conversationId?: string;
  busy = false;
  bound = false;
  statusLine = 'Checking Codegraph...';
  indexReady = false;
  copyHint = '';

  readonly suggestions: Suggestion[] = [
    { label: 'Index status', prompt: 'index status', mode: 'status' },
    { label: 'Show files', prompt: 'show files', mode: 'files' },
    { label: 'Find a symbol', prompt: 'tell me about ' },
    { label: 'Who calls…', prompt: 'who calls "' },
    { label: 'Impact of…', prompt: 'impact of "' }
  ];

  messages: UiMessage[] = [
    {
      id: this.nextId(),
      role: 'system',
      content:
        'Bind a mapped repository path, then ask about a topic. Try: tell me about IndexingService · who calls "CodegraphClient" · impact of EvidenceGraph · index status.',
      at: new Date()
    }
  ];

  get canChat(): boolean {
    return this.bound && !this.busy;
  }

  ngOnInit(): void {
    const saved = localStorage.getItem(REPO_KEY);
    if (saved) {
      this.repoPath = saved;
    }

    this.api.health().subscribe({
      next: (h) => {
        this.statusLine = h.healthy
          ? `Codegraph OK (${h.codegraph.versionOrDetail ?? 'ready'})`
          : `Codegraph missing — ${h.installHint}`;
      },
      error: () => {
        this.statusLine = 'API unreachable. Start CodegraphChat.Api on :5091.';
      }
    });

    this.api.getSession().subscribe({
      next: (s) => {
        if (s.repositoryPath) {
          this.repoPath = s.repositoryPath;
          this.bound = true;
          this.indexReady = s.indexReady;
          this.statusLine += s.indexReady ? ' · index ready' : ' · index not detected';
          localStorage.setItem(REPO_KEY, s.repositoryPath);
        } else if (saved) {
          this.bindRepo(true);
        }
      }
    });
  }

  bindRepo(quiet = false): void {
    if (!this.repoPath.trim()) {
      return;
    }
    this.busy = true;
    this.api.setSession(this.repoPath.trim()).subscribe({
      next: (s) => {
        this.bound = true;
        this.indexReady = s.indexReady;
        localStorage.setItem(REPO_KEY, s.repositoryPath ?? this.repoPath.trim());
        this.statusLine = s.indexReady
          ? `Bound to ${s.repositoryPath} (index ready)`
          : `Bound to ${s.repositoryPath} (no .codegraph index — use Ensure index)`;
        if (!quiet) {
          this.push('system', this.statusLine);
        }
        this.busy = false;
      },
      error: (err) => {
        this.bound = false;
        this.push('system', err?.error?.message ?? 'Failed to bind repository path.');
        this.busy = false;
      }
    });
  }

  ensureIndex(): void {
    if (!this.bound) {
      this.push('system', 'Bind a repository path first.');
      return;
    }
    this.busy = true;
    this.push('system', 'Ensuring Codegraph index (init or sync)…');
    this.api.ensureIndex().subscribe({
      next: (s) => {
        this.indexReady = s.indexReady;
        const ok = s.ensureSucceeded !== false;
        this.statusLine = ok
          ? `Index ${s.indexReady ? 'ready' : 'updated'} at ${s.repositoryPath}`
          : `Ensure index failed at ${s.repositoryPath}`;
        this.push(
          'system',
          ok
            ? `Ensure index OK. ${s.ensureDetail ?? ''}`.trim()
            : `Ensure index failed. ${s.ensureDetail ?? ''}`.trim()
        );
        this.busy = false;
      },
      error: (err) => {
        this.push('system', err?.error?.message ?? 'Ensure index failed.');
        this.busy = false;
      }
    });
  }

  useSuggestion(s: Suggestion): void {
    if (s.mode) {
      this.mode = s.mode;
    }
    this.draft = s.prompt;
    queueMicrotask(() => this.composer?.nativeElement.focus());
  }

  askFollowUp(symbol: string, kind: 'query' | 'callers' | 'callees' | 'impact' = 'query'): void {
    if (!symbol || this.busy) {
      return;
    }
    if (kind === 'callers') {
      this.mode = 'callers';
      this.draft = `who calls "${symbol}"`;
    } else if (kind === 'callees') {
      this.mode = 'callees';
      this.draft = `what does "${symbol}" call`;
    } else if (kind === 'impact') {
      this.mode = 'impact';
      this.draft = `impact of "${symbol}"`;
    } else {
      this.mode = 'query';
      this.draft = `tell me about ${symbol}`;
    }
    this.send();
  }

  send(): void {
    const text = this.draft.trim();
    if (!text || this.busy) {
      return;
    }
    if (!this.bound) {
      this.push('system', 'Bind a mapped repository path before chatting.');
      return;
    }

    this.draft = '';
    this.push('user', text);
    this.busy = true;

    this.api.chat(text, this.conversationId, this.mode || undefined).subscribe({
      next: (res) => {
        this.conversationId = res.conversationId;
        this.messages.push({
          id: this.nextId(),
          role: 'assistant',
          content: res.reply.content,
          at: new Date(res.reply.at),
          mode: res.detectedMode,
          symbol: res.detectedSymbol,
          evidence: res.evidence
        });
        this.busy = false;
        queueMicrotask(() => this.scrollToBottom());
      },
      error: (err) => {
        this.push('system', err?.error?.message ?? 'Chat request failed.');
        this.busy = false;
      }
    });
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  clearChat(): void {
    this.conversationId = undefined;
    this.messages = [
      {
        id: this.nextId(),
        role: 'system',
        content: 'Conversation cleared. Ask another topic.',
        at: new Date()
      }
    ];
  }

  copyMessage(content: string): void {
    navigator.clipboard?.writeText(content).then(
      () => {
        this.copyHint = 'Copied';
        setTimeout(() => (this.copyHint = ''), 1500);
      },
      () => {
        this.copyHint = 'Copy failed';
        setTimeout(() => (this.copyHint = ''), 1500);
      }
    );
  }

  private push(role: UiMessage['role'], content: string): void {
    this.messages.push({ id: this.nextId(), role, content, at: new Date() });
    queueMicrotask(() => this.scrollToBottom());
  }

  private scrollToBottom(): void {
    const el = this.scrollHost?.nativeElement;
    if (el) {
      el.scrollTop = el.scrollHeight;
    }
  }

  private nextId(): string {
    this.seq += 1;
    return `m-${this.seq}-${Date.now()}`;
  }
}
