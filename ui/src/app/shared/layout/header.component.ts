import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import { TranslocoPipe } from '@jsverse/transloco';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatSelectModule } from '@angular/material/select';

@Component({
  selector: 'hb-header',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslocoPipe, MatToolbarModule, MatButtonModule, MatSelectModule],
  template: `
    <mat-toolbar color="primary" class="shell-header">
      <span class="brand">{{ 'title' | transloco }}</span>
      <nav class="nav" aria-label="Primary">
        <a mat-button routerLink="/search">{{ 'search.title' | transloco }}</a>
        @if (auth.user(); as user) {
          @if (auth.hasPermission('MyListings.Create')) {
            <a mat-button routerLink="/broker/listings">{{ 'broker.listingsTitle' | transloco }}</a>
          }
          @if (auth.hasPermission('CommissionRates')) {
            <a mat-button routerLink="/admin/commission-rates">{{ 'admin.title' | transloco }}</a>
          }
        }
      </nav>
      <span class="spacer"></span>
      <mat-select
        class="lang-select"
        [value]="lang()"
        (selectionChange)="setLanguage($event.value)"
        [attr.aria-label]="'header.language' | transloco"
      >
        <!-- Explicit trigger label: mat-select otherwise shows the projected option text, which can stay stale while transloco loads the new language. -->
        <mat-select-trigger>{{ languageLabelKey(lang()) | transloco }}</mat-select-trigger>
        <mat-option value="en">{{ 'header.english' | transloco }}</mat-option>
        <mat-option value="ne">{{ 'header.nepali' | transloco }}</mat-option>
      </mat-select>
      @if (auth.user(); as user) {
        <span class="user">{{ user.name ?? ('header.account' | transloco) }}</span>
        <button mat-button (click)="auth.signOut()">{{ 'header.signOut' | transloco }}</button>
      } @else {
        <button mat-button (click)="auth.signIn()">{{ 'header.signIn' | transloco }}</button>
      }
    </mat-toolbar>
  `,
  styles: `
    .shell-header {
      flex-wrap: wrap;
      gap: 0.5rem;
    }
    .brand {
      font-weight: 600;
    }
    .spacer {
      flex: 1 1 auto;
    }
    .nav {
      display: flex;
      gap: 0.25rem;
    }
    .lang-select {
      width: 7rem;
    }
    .user {
      margin-inline-start: 0.5rem;
      font-size: 0.9rem;
    }
  `,
})
export class HeaderComponent {
  private readonly languageService = inject(LanguageService);
  readonly auth = inject(AuthService);
  readonly lang = this.languageService.currentLang;

  languageLabelKey(lang: 'en' | 'ne'): 'header.english' | 'header.nepali' {
    return lang === 'ne' ? 'header.nepali' : 'header.english';
  }

  setLanguage(value: string): void {
    if (value === 'en' || value === 'ne') {
      this.languageService.setLanguage(value as 'en' | 'ne');
    }
  }
}
