import { Component, inject, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'hb-header',
  standalone: true,
  template: `
    <header class="shell-header">
      <h1>House Broker</h1>
      <label class="lang">
        Language
        <select [value]="lang()" (change)="setLanguage($event)">
          <option value="en">English</option>
          <option value="ne">नेपाली</option>
        </select>
      </label>
    </header>
  `,
  styles: `
    .shell-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 1rem;
    }
    .lang {
      display: flex;
      gap: 0.5rem;
      align-items: center;
    }
  `,
})
export class HeaderComponent {
  private readonly transloco = inject(TranslocoService);
  readonly lang = signal(this.transloco.getActiveLang());

  setLanguage(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.transloco.setActiveLang(value);
    this.lang.set(value);
  }
}
