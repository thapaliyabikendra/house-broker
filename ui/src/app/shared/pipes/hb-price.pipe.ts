import type { PipeTransform } from '@angular/core';
import { inject, Pipe } from '@angular/core';
import { LanguageService } from '../../core/i18n/language.service';
import { MetaService } from '../../core/data/meta.service';

@Pipe({ name: 'hbPrice', standalone: true, pure: false })
export class HbPricePipe implements PipeTransform {
  private readonly language = inject(LanguageService);
  private readonly meta = inject(MetaService);

  transform(value: number | string | null | undefined, options?: Intl.NumberFormatOptions): string {
    const number = typeof value === 'string' ? Number(value) : value;
    if (number === null || number === undefined || Number.isNaN(number)) {
      return '';
    }
    return new Intl.NumberFormat(this.language.toFormattingLocale(), {
      style: 'currency',
      currency: this.meta.currencyCode(),
      ...options,
    }).format(number);
  }
}
