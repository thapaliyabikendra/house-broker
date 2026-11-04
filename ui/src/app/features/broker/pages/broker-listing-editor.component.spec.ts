import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';
import { Injectable } from '@angular/core';
import type { TranslocoLoader } from '@jsverse/transloco';
import { BrokerListingEditorComponent } from './broker-listing-editor.component';
import { ListingsDataService } from '../../../core/data/listings-data.service';
import { LookupsDataService } from '../../../core/data/lookups-data.service';
import { provideTransloco } from '@jsverse/transloco';

@Injectable()
class TestLoader implements TranslocoLoader {
  getTranslation() {
    return of({});
  }
}

describe('BrokerListingEditorComponent', () => {
  it('loads property types and stays in create mode without an id', async () => {
    const listingsData = {
      getMineById: vi.fn(),
      create: vi.fn().mockResolvedValue({ id: '1' }),
      update: vi.fn(),
    };
    const lookupsData = {
      propertyTypes: vi.fn().mockResolvedValue([{ id: 't1', name: 'House' }]),
    };

    await TestBed.configureTestingModule({
      imports: [BrokerListingEditorComponent],
      providers: [
        { provide: ListingsDataService, useValue: listingsData },
        { provide: LookupsDataService, useValue: lookupsData },
        {
          provide: ActivatedRoute,
          useValue: { paramMap: of(new Map()), snapshot: { paramMap: { get: () => null } } },
        },
        { provide: Router, useValue: { navigate: vi.fn() } },
        provideTransloco({
          config: { availableLangs: ['en', 'ne'], defaultLang: 'en' },
          loader: TestLoader,
        }),
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(BrokerListingEditorComponent);
    await fixture.whenStable();

    expect(fixture.componentInstance.isEdit()).toBe(false);
    expect(fixture.componentInstance.propertyTypes()).toEqual([{ id: 't1', name: 'House' }]);
  });
});
