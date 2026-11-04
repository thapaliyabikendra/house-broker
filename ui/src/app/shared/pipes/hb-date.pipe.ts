import type { PipeTransform } from '@angular/core';
import { inject, Pipe } from '@angular/core';
import { ClockService } from '../../core/time/clock.service';
import { TranslocoLocaleService } from '@jsverse/transloco-locale';

@Pipe({ name: 'hbDate', standalone: true, pure: false })
export class HbDatePipe implements PipeTransform {
  private readonly locale = inject(TranslocoLocaleService);
  private readonly clock = inject(ClockService);

  transform(value: string | Date | null | undefined, options?: Intl.DateTimeFormatOptions): string {
    const date = this.toDate(value);
    if (!date) {
      return '';
    }
    return new Intl.DateTimeFormat(this.locale.getLocale(), {
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
