import type { PipeTransform } from '@angular/core';
import { inject, Pipe } from '@angular/core';
import { TranslocoLocaleService } from '@jsverse/transloco-locale';

@Pipe({ name: 'hbNumber', standalone: true, pure: false })
export class HbNumberPipe implements PipeTransform {
  private readonly locale = inject(TranslocoLocaleService);

  transform(value: number | string | null | undefined, options?: Intl.NumberFormatOptions): string {
    const number = typeof value === 'string' ? Number(value) : value;
    if (number === null || number === undefined || Number.isNaN(number)) {
      return '';
    }
    return new Intl.NumberFormat(this.locale.getLocale(), options).format(number);
  }
}
