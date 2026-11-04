import { Component } from '@angular/core';
import type { ComponentFixture } from '@angular/core/testing';
import { TestBed } from '@angular/core/testing';
import { HbPriceShortPipe } from './hb-price-short.pipe';
import { LanguageService } from '../../core/i18n/language.service';
import { MetaService } from '../../core/data/meta.service';

@Component({
  imports: [HbPriceShortPipe],
  template: `{{ value | hbPriceShort }}`,
})
class HostComponent {
  value: number | string | null = 1234567;
}

describe('HbPriceShortPipe', () => {
  let fixture: ComponentFixture<HostComponent>;

  beforeEach(async () => {
    const languageService = { toFormattingLocale: () => 'en-IN' };
    const metaService = { currencyCode: () => 'NPR' };

    await TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [
        { provide: LanguageService, useValue: languageService },
        { provide: MetaService, useValue: metaService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(HostComponent);
  });

  it('formats compact currency', () => {
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent?.trim() ?? '';
    expect(text.length).toBeGreaterThan(0);
    expect(text).toContain('12');
  });

  it('returns empty for invalid values', () => {
    fixture.componentInstance.value = null;
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent?.trim()).toBe('');

    fixture.componentInstance.value = NaN;
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent?.trim()).toBe('');
  });
});
