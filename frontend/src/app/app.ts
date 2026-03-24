import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ExplorerPageComponent } from './features/explorer/pages/explorer-page/explorer-page.component';
import { Sidebar } from './shared/components/sidebar/sidebar';
import { LoginModal } from './shared/components/login-modal/login-modal';
import { AboutModalComponent } from './shared/components/about-modal/about-modal';
import { AuthStateService } from './core/auth/auth-state.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, ExplorerPageComponent, Sidebar, LoginModal, AboutModalComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit {
  protected readonly authState = inject(AuthStateService);
  isLoginModalOpen = false;
  isFocusMode = false;
  isAboutModalOpen = false;
  readonly isResetPasswordView = window.location.pathname === '/reset-password';
  readonly isConfirmEmailView = window.location.pathname === '/confirm-email';
  readonly resetEmail = new URLSearchParams(window.location.search).get('email') ?? '';
  readonly resetToken = new URLSearchParams(window.location.search).get('token') ?? '';
  readonly confirmUserId = new URLSearchParams(window.location.search).get('userId') ?? '';
  readonly confirmToken = new URLSearchParams(window.location.search).get('token') ?? '';
  confirmStatus: 'pending' | 'success' | 'error' = 'pending';
  confirmMessage = 'Confirming your email...';

  ngOnInit(): void {
    if (!this.isConfirmEmailView) {
      return;
    }

    if (!this.confirmUserId || !this.confirmToken) {
      this.confirmStatus = 'error';
      this.confirmMessage = 'Confirmation link is invalid or incomplete.';
      return;
    }

    this.authState.confirmEmail({
      userId: this.confirmUserId,
      token: this.confirmToken
    }).subscribe({
      next: () => {
        this.confirmStatus = 'success';
        this.confirmMessage = 'Email confirmed. Signing you in...';
        setTimeout(() => {
          window.location.replace('/');
        }, 450);
      },
      error: (error) => {
        this.confirmStatus = 'error';
        this.confirmMessage = this.extractConfirmError(error);
      }
    });
  }

  toggleLoginModal() {
    this.isLoginModalOpen = !this.isLoginModalOpen;
  }

  toggleFocusMode() {
    this.isFocusMode = !this.isFocusMode;
  }

  toggleAboutModal() {
    this.isAboutModalOpen = !this.isAboutModalOpen;
  }

  signOut(): void {
    this.authState.logout();
    this.isLoginModalOpen = false;
  }

  onAuthenticated(): void {
    this.isLoginModalOpen = false;
  }

  openLoginFromConfirmation(): void {
    window.location.href = '/';
  }

  private extractConfirmError(error: unknown): string {
    if (typeof error === 'object' && error !== null && 'error' in error) {
      const payload = (error as { error: unknown }).error;

      if (typeof payload === 'string' && payload.trim().length > 0) {
        return payload;
      }

      if (typeof payload === 'object' && payload !== null && 'errors' in payload) {
        const errors = (payload as { errors?: unknown }).errors;
        if (Array.isArray(errors) && errors.length > 0) {
          return String(errors[0]);
        }
      }
    }

    return 'Unable to confirm email. Please request a new confirmation link.';
  }
}
