import type { PipeTransform } from '@angular/core';
import { inject, Pipe } from '@angular/core';
import { ClockService } from '../../core/time/clock.service';
import { LanguageService } from '../../core/i18n/language.service';

@Pipe({ name: 'hbDate', standalone: true, pure: false })
export class HbDatePipe implements PipeTransform {
  private readonly language = inject(LanguageService);
  private readonly clock = inject(ClockService);

  transform(value: string | Date | null | undefined, options?: Intl.DateTimeFormatOptions): string {
    const date = this.toDate(value);
    if (!date) {
      return '';
    }
    return new Intl.DateTimeFormat(this.language.toFormattingLocale(), {
      timeZone: this.clock.timeZone(),
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      ...options,
    }).format(date);
  }

  private toDate(value: string | Date | null | undefined): Date | null {
    if (value === null || value === undefined) {
      return null;
    }
    const date = value instanceof Date ? value : new Date(value);
    return Number.isNaN(date.getTime()) ? null : date;
  }
}
