import { Component, ElementRef, OnInit, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ChatApiService, ChatEvidenceDto } from '../services/chat-api.service';

interface UiMessage {
  role: 'user' | 'assistant' | 'system';
  content: string;
  at: Date;
  mode?: string;
  evidence?: ChatEvidenceDto[];
}

@Component({
  selector: 'app-chat-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './chat-page.component.html',
  styleUrl: './chat-page.component.css'
})
export class ChatPageComponent implements OnInit {
  @ViewChild('scrollHost') scrollHost?: ElementRef<HTMLDivElement>;

  private readonly api = inject(ChatApiService);

  repoPath = '';
  draft = '';
  mode = '';
  conversationId?: string;
  busy = false;
  statusLine = 'Checking Codegraph...';
  indexReady = false;
  messages: UiMessage[] = [
    {
      role: 'system',
      content:
        'Ask about a topic in the indexed project. Examples: "tell me about IndexingService", "who calls CodegraphClient", "impact of EvidenceGraph", "index status".',
      at: new Date()
    }
  ];

  ngOnInit(): void {
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
        this.repoPath = s.repositoryPath ?? '';
        this.indexReady = s.indexReady;
        if (s.repositoryPath) {
          this.statusLine += s.indexReady ? ' · index ready' : ' · index not detected';
        }
      }
    });
  }

  bindRepo(): void {
    if (!this.repoPath.trim()) {
      return;
    }
    this.busy = true;
    this.api.setSession(this.repoPath.trim()).subscribe({
      next: (s) => {
        this.indexReady = s.indexReady;
        this.statusLine = s.indexReady
          ? `Bound to ${s.repositoryPath} (index ready)`
          : `Bound to ${s.repositoryPath} (no .codegraph index detected — run codegraph init first)`;
        this.push('system', this.statusLine);
        this.busy = false;
      },
      error: (err) => {
        this.push('system', err?.error?.message ?? 'Failed to bind repository path.');
        this.busy = false;
      }
    });
  }

  send(): void {
    const text = this.draft.trim();
    if (!text || this.busy) {
      return;
    }
    this.draft = '';
    this.push('user', text);
    this.busy = true;

    this.api.chat(text, this.conversationId, this.mode || undefined).subscribe({
      next: (res) => {
        this.conversationId = res.conversationId;
        this.messages.push({
          role: 'assistant',
          content: res.reply.content,
          at: new Date(res.reply.at),
          mode: res.detectedMode,
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
        role: 'system',
        content: 'Conversation cleared. Ask another topic.',
        at: new Date()
      }
    ];
  }

  private push(role: UiMessage['role'], content: string): void {
    this.messages.push({ role, content, at: new Date() });
    queueMicrotask(() => this.scrollToBottom());
  }

  private scrollToBottom(): void {
    const el = this.scrollHost?.nativeElement;
    if (el) {
      el.scrollTop = el.scrollHeight;
    }
  }
}
