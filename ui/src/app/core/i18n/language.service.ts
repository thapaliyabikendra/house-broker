import { effect, inject, Injectable, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';

type SupportedLang = 'en' | 'ne';

@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly transloco = inject(TranslocoService);
  readonly currentLang = signal<SupportedLang>(this.initialLanguage());

  constructor() {
    this.transloco.setActiveLang(this.currentLang());
    effect(() => {
      const lang = this.currentLang();
      this.transloco.setActiveLang(lang);
      document.documentElement.lang = lang;
      try {
        localStorage.setItem('hb.lang', lang);
      } catch {
        // ignore storage failures
      }
    });
  }

  setLanguage(lang: SupportedLang): void {
    this.currentLang.set(lang);
  }

  toFormattingLocale(lang: SupportedLang = this.currentLang()): string {
    return lang === 'ne' ? 'ne-NP' : 'en-IN';
  }

  private initialLanguage(): SupportedLang {
    try {
      const saved = localStorage.getItem('hb.lang');
      if (saved === 'en' || saved === 'ne') {
        return saved;
      }
    } catch {
      // ignore storage failures
    }

    const browser = (navigator.language ?? 'en').toLowerCase();
    if (browser.startsWith('ne')) {
      return 'ne';
    }
    return 'en';
  }
}
