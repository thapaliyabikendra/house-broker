import type { PipeTransform } from '@angular/core';
import { inject, Pipe } from '@angular/core';
import { ClockService } from '../../core/time/clock.service';
import { TranslocoLocaleService } from '@jsverse/transloco-locale';

@Pipe({ name: 'hbRelativeTime', standalone: true, pure: false })
export class HbRelativeTimePipe implements PipeTransform {
  private readonly locale = inject(TranslocoLocaleService);
  private readonly clock = inject(ClockService);

  transform(value: string | Date | null | undefined): string {
    const date = this.toDate(value);
    if (!date) {
      return '';
    }
    const diffSeconds = Math.round((date.getTime() - this.clock.now().getTime()) / 1000);
    const { amount, unit } = this.toUnit(diffSeconds);
    return new Intl.RelativeTimeFormat(this.locale.getLocale(), {
      numeric: 'auto',
    }).format(amount, unit);
  }

  private toDate(value: string | Date | null | undefined): Date | null {
    if (value === null || value === undefined) {
      return null;
    }
    const date = value instanceof Date ? value : new Date(value);
    return Number.isNaN(date.getTime()) ? null : date;
  }

  private toUnit(diffSeconds: number): { amount: number; unit: Intl.RelativeTimeFormatUnit } {
    const abs = Math.abs(diffSeconds);
    if (abs < 60) {
      return { amount: diffSeconds, unit: 'second' };
    }
    if (abs < 3600) {
      return { amount: Math.round(diffSeconds / 60), unit: 'minute' };
    }
    if (abs < 86400) {
      return { amount: Math.round(diffSeconds / 3600), unit: 'hour' };
    }
    if (abs < 2592000) {
      return { amount: Math.round(diffSeconds / 86400), unit: 'day' };
    }
    if (abs < 31536000) {
      return { amount: Math.round(diffSeconds / 2592000), unit: 'month' };
    }
    return { amount: Math.round(diffSeconds / 31536000), unit: 'year' };
  }
}
