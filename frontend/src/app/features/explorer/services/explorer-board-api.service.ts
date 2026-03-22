import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface PositionMoveRequest {
  fen: string;
  from: string;
  to: string;
  promotion?: string | null;
}

export interface PositionMoveResponse {
  isValid: boolean;
  fen?: string | null;
  san?: string | null;
  error?: string | null;
}

@Injectable({ providedIn: 'root' })
export class ExplorerBoardApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = this.resolveBaseUrl();

  applyMove(request: PositionMoveRequest): Observable<PositionMoveResponse> {
    return this.http.post<PositionMoveResponse>(`${this.baseUrl}/position/move`, request);
  }

  private resolveBaseUrl(): string {
    const host = window.location.hostname;
    const isLocalHost = host === 'localhost' || host === '127.0.0.1' || host === '::1';

    if (isLocalHost) {
      return `http://${host}:5027/api/games/explorer`;
    }

    return '/api/games/explorer';
  }
}
