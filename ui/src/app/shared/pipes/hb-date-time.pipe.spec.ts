import { Component } from '@angular/core';
import type { ComponentFixture } from '@angular/core/testing';
import { TestBed } from '@angular/core/testing';
import { HbDateTimePipe } from './hb-date-time.pipe';
import { TranslocoLocaleService } from '@jsverse/transloco-locale';
import { ClockService } from '../../core/time/clock.service';

@Component({
  imports: [HbDateTimePipe],
  template: `{{ value | hbDateTime }}`,
})
class HostComponent {
  value: string | null = '2026-02-03T12:34:00Z';
}

describe('HbDateTimePipe', () => {
  let fixture: ComponentFixture<HostComponent>;

  beforeEach(async () => {
    const localeService = { getLocale: () => 'en-IN' };
    const clockService = { timeZone: () => 'UTC' };

    await TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [
        { provide: TranslocoLocaleService, useValue: localeService },
        { provide: ClockService, useValue: clockService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(HostComponent);
  });

  it('formats a valid date-time', () => {
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent?.trim() ?? '';
    expect(text).toContain('2026');
    expect(text.length).toBeGreaterThan(0);
  });

  it('returns empty for invalid input', () => {
    fixture.componentInstance.value = 'invalid';
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent?.trim()).toBe('');
  });
});
