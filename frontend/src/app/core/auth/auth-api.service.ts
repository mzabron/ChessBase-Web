import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  AuthLoginRequest,
  AuthRegisterRequest,
  AuthTokenResponse,
  ForgotPasswordRequest,
  ResetPasswordRequest
} from './auth.models';

@Injectable({ providedIn: 'root' })
export class AuthApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = this.resolveBaseUrl();

  register(request: AuthRegisterRequest): Observable<AuthTokenResponse> {
    return this.http.post<AuthTokenResponse>(`${this.baseUrl}/auth/register`, request);
  }

  login(request: AuthLoginRequest): Observable<AuthTokenResponse> {
    return this.http.post<AuthTokenResponse>(`${this.baseUrl}/auth/login`, request);
  }

  forgotPassword(request: ForgotPasswordRequest): Observable<string> {
    return this.http.post(`${this.baseUrl}/auth/forgot-password`, request, {
      responseType: 'text'
    });
  }

  resetPassword(request: ResetPasswordRequest): Observable<string> {
    return this.http.post(`${this.baseUrl}/auth/reset-password`, request, {
      responseType: 'text'
    });
  }

  private resolveBaseUrl(): string {
    const host = window.location.hostname;
    const isLocalHost = host === 'localhost' || host === '127.0.0.1' || host === '::1';

    if (isLocalHost) {
      return `http://${host}:5027/api`;
    }

    return '/api';
  }
}
