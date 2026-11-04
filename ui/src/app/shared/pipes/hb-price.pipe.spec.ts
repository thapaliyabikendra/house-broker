import { Component } from '@angular/core';
import type { ComponentFixture } from '@angular/core/testing';
import { TestBed } from '@angular/core/testing';
import { TranslocoService } from '@jsverse/transloco';
import { HbPricePipe } from './hb-price.pipe';
import { LanguageService } from '../../core/i18n/language.service';
import { MetaService } from '../../core/data/meta.service';

@Component({
  imports: [HbPricePipe],
  template: `{{ value | hbPrice }}`,
})
class HostComponent {
  value = 1234.5;
}

describe('HbPricePipe', () => {
  let fixture: ComponentFixture<HostComponent>;

  beforeEach(async () => {
    const languageService = {
      currentLang: () => 'en' as const,
      toFormattingLocale: () => 'en-IN',
    };
    const metaService = { currencyCode: () => 'NPR' };

    await TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [
        { provide: LanguageService, useValue: languageService },
        { provide: MetaService, useValue: metaService },
        { provide: TranslocoService, useValue: { setActiveLang: () => undefined } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(HostComponent);
  });

  it('formats a number as NPR currency', () => {
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent?.trim()).toContain('1,234.50');
  });
});
