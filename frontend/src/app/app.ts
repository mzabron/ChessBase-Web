import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ExplorerPageComponent } from './features/explorer/pages/explorer-page/explorer-page.component';
import { Sidebar } from './shared/components/sidebar/sidebar';
import { LoginModal } from './shared/components/login-modal/login-modal';
import { AboutModalComponent } from './shared/components/about-modal/about-modal';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, ExplorerPageComponent, Sidebar, LoginModal, AboutModalComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  isLoginModalOpen = false;
  isFocusMode = false;
  isAboutModalOpen = false;

  toggleLoginModal() {
    this.isLoginModalOpen = !this.isLoginModalOpen;
  }

  toggleFocusMode() {
    this.isFocusMode = !this.isFocusMode;
  }

  toggleAboutModal() {
    this.isAboutModalOpen = !this.isAboutModalOpen;
  }
}
