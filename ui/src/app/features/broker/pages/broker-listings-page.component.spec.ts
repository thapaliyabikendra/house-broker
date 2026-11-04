import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { of } from 'rxjs';
import { Injectable } from '@angular/core';
import type { TranslocoLoader } from '@jsverse/transloco';
import { ActivatedRoute, Router } from '@angular/router';
import { BrokerListingsPageComponent } from './broker-listings-page.component';
import { ListingsDataService } from '../../../core/data/listings-data.service';
import { RuntimeConfigService } from '../../../core/config/runtime-config.service';
import { provideTransloco } from '@jsverse/transloco';
import {
  provideTranslocoLocale,
  provideTranslocoLocaleLangMapping,
} from '@jsverse/transloco-locale';

@Injectable()
class TestLoader implements TranslocoLoader {
  getTranslation() {
    return of({});
  }
}

describe('BrokerListingsPageComponent', () => {
  it('loads owned listings on init', async () => {
    const listingsData = {
      getMine: vi.fn().mockResolvedValue([{ id: '1' }]),
      getMineById: vi
        .fn()
        .mockResolvedValue({ id: '1', title: 'Listing', price: 1000, images: [] }),
      delete: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [BrokerListingsPageComponent],
      providers: [
        { provide: ListingsDataService, useValue: listingsData },
        { provide: RuntimeConfigService, useValue: { value: () => ({ apiBaseUrl: '/api' }) } },
        provideTransloco({
          config: { availableLangs: ['en', 'ne'], defaultLang: 'en' },
          loader: TestLoader,
        }),
        provideTranslocoLocale(),
        provideTranslocoLocaleLangMapping({ en: 'en-IN', ne: 'ne-NP' }),
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: ActivatedRoute, useValue: { snapshot: {} } },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(BrokerListingsPageComponent);
    await fixture.whenStable();

    expect(listingsData.getMine).toHaveBeenCalled();
    expect(fixture.componentInstance.listings()).toHaveLength(1);
  });

  it('filters a listing out after delete is confirmed', async () => {
    const listingsData = {
      getMine: vi.fn().mockResolvedValue([{ id: '1' }]),
      getMineById: vi
        .fn()
        .mockResolvedValue({ id: '1', title: 'Listing', price: 1000, images: [] }),
      delete: vi.fn().mockResolvedValue(undefined),
    };
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);

    await TestBed.configureTestingModule({
      imports: [BrokerListingsPageComponent],
      providers: [
        { provide: ListingsDataService, useValue: listingsData },
        { provide: RuntimeConfigService, useValue: { value: () => ({ apiBaseUrl: '/api' }) } },
        provideTransloco({
          config: { availableLangs: ['en', 'ne'], defaultLang: 'en' },
          loader: TestLoader,
        }),
        provideTranslocoLocale(),
        provideTranslocoLocaleLangMapping({ en: 'en-IN', ne: 'ne-NP' }),
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: ActivatedRoute, useValue: { snapshot: {} } },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(BrokerListingsPageComponent);
    await fixture.whenStable();

    await fixture.componentInstance.remove('1');

    expect(listingsData.delete).toHaveBeenCalledWith('1');
    expect(fixture.componentInstance.listings()).toHaveLength(0);
    confirmSpy.mockRestore();
  });
});
