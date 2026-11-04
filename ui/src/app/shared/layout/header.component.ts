import { Component, inject, signal } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'hb-header',
  standalone: true,
  imports: [TranslocoPipe],
  template: `
    <header class="shell-header">
      <h1>{{ 'title' | transloco }}</h1>
      <div class="actions">
        <label class="lang">
          Language
          <select [value]="lang()" (change)="setLanguage($event)">
            <option value="en">English</option>
            <option value="ne">नेपाली</option>
          </select>
        </label>
        @if (auth.user(); as user) {
          <span class="user">{{ user.name ?? user.sub }}</span>
          <button type="button" (click)="auth.signOut()">Sign out</button>
        } @else {
          <button type="button" (click)="auth.signIn()">Sign in</button>
        }
      </div>
    </header>
  `,
  styles: `
    .shell-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 1rem;
    }
    .actions {
      display: flex;
      gap: 0.75rem;
      align-items: center;
    }
    .lang {
      display: flex;
      gap: 0.5rem;
      align-items: center;
    }
  `,
})
export class HeaderComponent {
  private readonly languageService = inject(LanguageService);
  readonly auth = inject(AuthService);
  readonly lang = signal(this.languageService.currentLang());

  setLanguage(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    if (value === 'en' || value === 'ne') {
      this.languageService.setLanguage(value);
    }
    this.lang.set(this.languageService.currentLang());
  }
}
