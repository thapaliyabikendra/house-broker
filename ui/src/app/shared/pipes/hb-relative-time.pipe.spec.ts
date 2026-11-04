import { Component } from '@angular/core';
import type { ComponentFixture } from '@angular/core/testing';
import { TestBed } from '@angular/core/testing';
import { HbRelativeTimePipe } from './hb-relative-time.pipe';
import { LanguageService } from '../../core/i18n/language.service';
import { ClockService } from '../../core/time/clock.service';

@Component({
  imports: [HbRelativeTimePipe],
  template: `{{ value | hbRelativeTime }}`,
})
class HostComponent {
  value: string | null = '2026-02-03T12:00:00Z';
}

describe('HbRelativeTimePipe', () => {
  let fixture: ComponentFixture<HostComponent>;

  beforeEach(async () => {
    const languageService = { toFormattingLocale: () => 'en-IN' };
    const clock = { now: () => new Date('2026-02-03T11:00:00Z') };

    await TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [
        { provide: LanguageService, useValue: languageService },
        { provide: ClockService, useValue: clock },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(HostComponent);
  });

  it('formats a relative time', () => {
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent?.trim() ?? '';
    expect(text.length).toBeGreaterThan(0);
  });

  it('returns empty for invalid input', () => {
    fixture.componentInstance.value = 'invalid';
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent?.trim()).toBe('');
  });
});
