import type { PipeTransform } from '@angular/core';
import { inject, Pipe } from '@angular/core';
import { TranslocoLocaleService } from '@jsverse/transloco-locale';
import { MetaService } from '../../core/data/meta.service';

@Pipe({ name: 'hbPriceShort', standalone: true, pure: false })
export class HbPriceShortPipe implements PipeTransform {
  private readonly locale = inject(TranslocoLocaleService);
  private readonly meta = inject(MetaService);

  transform(value: number | string | null | undefined): string {
    const number = typeof value === 'string' ? Number(value) : value;
    if (number === null || number === undefined || Number.isNaN(number)) {
      return '';
    }
    return new Intl.NumberFormat(this.locale.getLocale(), {
      style: 'currency',
      currency: this.meta.currencyCode(),
      notation: 'compact',
      maximumFractionDigits: 2,
    }).format(number);
  }
}
