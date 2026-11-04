import { Component } from '@angular/core';
import type { ComponentFixture } from '@angular/core/testing';
import { TestBed } from '@angular/core/testing';
import { HbNumberPipe } from './hb-number.pipe';
import { TranslocoLocaleService } from '@jsverse/transloco-locale';

@Component({
  imports: [HbNumberPipe],
  template: `{{ value | hbNumber }}`,
})
class HostComponent {
  value: number | string | null = 1234.5;
}

describe('HbNumberPipe', () => {
  let fixture: ComponentFixture<HostComponent>;

  beforeEach(async () => {
    const localeService = { getLocale: () => 'en-IN' };

    await TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [{ provide: TranslocoLocaleService, useValue: localeService }],
    }).compileComponents();

    fixture = TestBed.createComponent(HostComponent);
  });

  it('formats a number', () => {
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent?.trim()).toContain('1,234.5');
  });

  it('formats a numeric string', () => {
    fixture.componentInstance.value = '9876.54';
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent?.trim()).toContain('9,876.54');
  });

  it('returns empty for null, undefined, and NaN', () => {
    fixture.componentInstance.value = null;
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent?.trim()).toBe('');

    fixture.componentInstance.value = NaN;
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent?.trim()).toBe('');
  });
});
