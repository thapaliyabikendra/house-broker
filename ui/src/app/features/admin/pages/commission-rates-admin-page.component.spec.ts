import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { of } from 'rxjs';
import { Injectable } from '@angular/core';
import type { TranslocoLoader } from '@jsverse/transloco';
import { provideTransloco } from '@jsverse/transloco';
import {
  provideTranslocoLocale,
  provideTranslocoLocaleLangMapping,
} from '@jsverse/transloco-locale';
import { CommissionRatesAdminPageComponent } from './commission-rates-admin-page.component';
import { AdminCommissionRatesDataService } from '../../../core/data/admin-commission-rates-data.service';

@Injectable()
class TestLoader implements TranslocoLoader {
  getTranslation() {
    return of({});
  }
}

describe('CommissionRatesAdminPageComponent', () => {
  it('loads commission rates on init', async () => {
    const data = {
      list: vi
        .fn()
        .mockResolvedValue([
          { id: '1', minPrice: 0, maxPrice: 100, ratePercent: 2, isActive: true },
        ]),
      create: vi.fn(),
      update: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [CommissionRatesAdminPageComponent],
      providers: [
        { provide: AdminCommissionRatesDataService, useValue: data },
        provideTransloco({
          config: { availableLangs: ['en', 'ne'], defaultLang: 'en' },
          loader: TestLoader,
        }),
        provideTranslocoLocale(),
        provideTranslocoLocaleLangMapping({ en: 'en-IN', ne: 'ne-NP' }),
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(CommissionRatesAdminPageComponent);
    await fixture.whenStable();

    expect(data.list).toHaveBeenCalled();
    expect(fixture.componentInstance.rates()).toHaveLength(1);
  });

  it('patches the form when editing a rate', async () => {
    const data = {
      list: vi
        .fn()
        .mockResolvedValue([
          { id: '1', minPrice: 0, maxPrice: 100, ratePercent: 2, isActive: true, version: 'v1' },
        ]),
      create: vi.fn(),
      update: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [CommissionRatesAdminPageComponent],
      providers: [
        { provide: AdminCommissionRatesDataService, useValue: data },
        provideTransloco({
          config: { availableLangs: ['en', 'ne'], defaultLang: 'en' },
          loader: TestLoader,
        }),
        provideTranslocoLocale(),
        provideTranslocoLocaleLangMapping({ en: 'en-IN', ne: 'ne-NP' }),
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(CommissionRatesAdminPageComponent);
    await fixture.whenStable();

    const rate = fixture.componentInstance.rates()[0];
    if (!rate) {
      throw new Error('expected a rate');
    }
    fixture.componentInstance.edit(rate);
    expect(fixture.componentInstance.editingId()).toBe('1');
    expect(fixture.componentInstance.form.value.minPrice).toBe(0);
  });
});
