import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface HealthDto {
  status: string;
  healthy: boolean;
  message: string;
  missing: string[];
  installHint: string;
  codegraph: { available: boolean; versionOrDetail?: string };
}

export interface SessionConfigDto {
  repositoryPath?: string;
  indexReady: boolean;
  indexDetail?: string;
  codegraphAvailable: boolean;
  codegraphVersion?: string;
  ensureSucceeded?: boolean;
  ensureDetail?: string;
}

export interface ChatMessageDto {
  role: string;
  content: string;
  at: string;
}

export interface ChatEvidenceDto {
  command: string;
  symbol?: string;
  succeeded: boolean;
  exitCode: number;
  output: string;
  error: string;
}

export interface ChatResponse {
  conversationId: string;
  reply: ChatMessageDto;
  evidence: ChatEvidenceDto[];
  detectedMode: string;
  detectedSymbol?: string;
}

@Injectable({ providedIn: 'root' })
export class ChatApiService {
  private readonly http = inject(HttpClient);

  health(): Observable<HealthDto> {
    return this.http.get<HealthDto>('/api/health');
  }

  getSession(): Observable<SessionConfigDto> {
    return this.http.get<SessionConfigDto>('/api/session');
  }

  setSession(repositoryPath: string): Observable<SessionConfigDto> {
    return this.http.post<SessionConfigDto>('/api/session', { repositoryPath });
  }

  ensureIndex(): Observable<SessionConfigDto> {
    return this.http.post<SessionConfigDto>('/api/session/ensure-index', {});
  }

  chat(message: string, conversationId?: string, mode?: string): Observable<ChatResponse> {
    return this.http.post<ChatResponse>('/api/chat', { message, conversationId, mode });
  }
}
