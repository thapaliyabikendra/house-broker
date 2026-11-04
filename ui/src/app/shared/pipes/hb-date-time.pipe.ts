import type { PipeTransform } from '@angular/core';
import { inject, Pipe } from '@angular/core';
import { ClockService } from '../../core/time/clock.service';
import { TranslocoLocaleService } from '@jsverse/transloco-locale';

@Pipe({ name: 'hbDateTime', standalone: true, pure: false })
export class HbDateTimePipe implements PipeTransform {
  private readonly locale = inject(TranslocoLocaleService);
  private readonly clock = inject(ClockService);

  // Impure so it follows locale and time-zone changes; the last result is memoized (see HbPricePipe).
  private lastKey: string | null = null;
  private lastResult = '';

  transform(value: string | Date | null | undefined, options?: Intl.DateTimeFormatOptions): string {
    const date = this.toDate(value);
    if (!date) {
      return '';
    }
    const locale = this.locale.getLocale();
    const timeZone = this.clock.timeZone();
    const key = `${locale}|${timeZone}|${date.getTime()}|${JSON.stringify(options ?? {})}`;
    if (key !== this.lastKey) {
      this.lastKey = key;
      this.lastResult = new Intl.DateTimeFormat(locale, {
        timeZone,
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: 'numeric',
        minute: '2-digit',
        ...options,
      }).format(date);
    }
    return this.lastResult;
  }

  private toDate(value: string | Date | null | undefined): Date | null {
    if (value === null || value === undefined) {
      return null;
    }
    const date = value instanceof Date ? value : new Date(value);
    return Number.isNaN(date.getTime()) ? null : date;
  }
}
