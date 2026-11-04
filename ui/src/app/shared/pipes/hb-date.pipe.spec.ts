import { Component } from '@angular/core';
import type { ComponentFixture } from '@angular/core/testing';
import { TestBed } from '@angular/core/testing';
import { HbDatePipe } from './hb-date.pipe';
import { LanguageService } from '../../core/i18n/language.service';
import { ClockService } from '../../core/time/clock.service';

@Component({
  imports: [HbDatePipe],
  template: `{{ value | hbDate }}`,
})
class HostComponent {
  value: string | null = '2026-02-03T00:00:00Z';
}

describe('HbDatePipe', () => {
  let fixture: ComponentFixture<HostComponent>;

  beforeEach(async () => {
    const languageService = { toFormattingLocale: () => 'en-IN' };
    const clockService = { timeZone: () => 'UTC' };

    await TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [
        { provide: LanguageService, useValue: languageService },
        { provide: ClockService, useValue: clockService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(HostComponent);
  });

  it('formats a valid date', () => {
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent?.trim()).toContain('2026');
  });

  it('returns empty for null, undefined, and invalid values', () => {
    fixture.componentInstance.value = null;
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent?.trim()).toBe('');

    fixture.componentInstance.value = 'not-a-date';
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent?.trim()).toBe('');
  });
});
