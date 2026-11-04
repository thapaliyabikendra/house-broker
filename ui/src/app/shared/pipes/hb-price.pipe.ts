import type { PipeTransform } from '@angular/core';
import { inject, Pipe } from '@angular/core';
import { TranslocoLocaleService } from '@jsverse/transloco-locale';
import { MetaService } from '../../core/data/meta.service';

@Pipe({ name: 'hbPrice', standalone: true, pure: false })
export class HbPricePipe implements PipeTransform {
  private readonly locale = inject(TranslocoLocaleService);
  private readonly meta = inject(MetaService);

  // Impure so it follows locale and currency changes; the last result is memoized so
  // Intl is not re-run on every change-detection pass.
  private lastKey: string | null = null;
  private lastResult = '';

  transform(value: number | string | null | undefined, options?: Intl.NumberFormatOptions): string {
    const number = typeof value === 'string' ? Number(value) : value;
    if (number === null || number === undefined || Number.isNaN(number)) {
      return '';
    }
    const locale = this.locale.getLocale();
    const currency = this.meta.currencyCode();
    const key = `${locale}|${currency}|${number}|${JSON.stringify(options ?? {})}`;
    if (key !== this.lastKey) {
      this.lastKey = key;
      this.lastResult = new Intl.NumberFormat(locale, {
        style: 'currency',
        currency,
        ...options,
      }).format(number);
    }
    return this.lastResult;
  }
}
