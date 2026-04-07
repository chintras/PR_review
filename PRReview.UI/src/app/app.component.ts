import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, MatToolbarModule, MatIconModule],
  template: `
    <mat-toolbar class="app-toolbar">
      <mat-icon class="toolbar-icon">rate_review</mat-icon>
      <span class="toolbar-title">PR Review</span>
      <span class="toolbar-subtitle">Azure DevOps · Claude AI</span>
    </mat-toolbar>
    <main class="app-main">
      <router-outlet />
    </main>
  `,
  styles: [`
    .app-toolbar {
      background: linear-gradient(135deg, #1a365d 0%, #2a4a7f 100%);
      color: white;
      gap: 12px;
      padding: 0 24px;
      position: sticky;
      top: 0;
      z-index: 100;
      box-shadow: 0 2px 8px rgba(0,0,0,0.2);
    }
    .toolbar-icon {
      font-size: 26px;
      width: 26px;
      height: 26px;
      opacity: 0.9;
    }
    .toolbar-title {
      font-size: 1.25rem;
      font-weight: 600;
      letter-spacing: 0.5px;
    }
    .toolbar-subtitle {
      font-size: 0.8rem;
      opacity: 0.6;
      margin-left: 8px;
      font-weight: 300;
    }
    .app-main {
      min-height: calc(100vh - 64px);
      padding: 32px 24px;
      max-width: 1400px;
      margin: 0 auto;
    }
  `]
})
export class AppComponent {}
