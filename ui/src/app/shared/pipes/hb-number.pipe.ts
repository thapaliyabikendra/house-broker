import type { PipeTransform } from '@angular/core';
import { inject, Pipe } from '@angular/core';
import { TranslocoLocaleService } from '@jsverse/transloco-locale';

@Pipe({ name: 'hbNumber', standalone: true, pure: false })
export class HbNumberPipe implements PipeTransform {
  private readonly locale = inject(TranslocoLocaleService);

  // Impure so it follows locale changes; the last result is memoized (see HbPricePipe).
  private lastKey: string | null = null;
  private lastResult = '';

  transform(value: number | string | null | undefined, options?: Intl.NumberFormatOptions): string {
    const number = typeof value === 'string' ? Number(value) : value;
    if (number === null || number === undefined || Number.isNaN(number)) {
      return '';
    }
    const locale = this.locale.getLocale();
    const key = `${locale}|${number}|${JSON.stringify(options ?? {})}`;
    if (key !== this.lastKey) {
      this.lastKey = key;
      this.lastResult = new Intl.NumberFormat(locale, options).format(number);
    }
    return this.lastResult;
  }
}
