import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { SearchStore } from './search.store';
import { ListingsDataService } from '../../../core/data/listings-data.service';
import { LookupsDataService } from '../../../core/data/lookups-data.service';
import { TranslocoService } from '@jsverse/transloco';

describe('SearchStore', () => {
  let store: SearchStore;

  const listingsData = {
    search: vi.fn().mockResolvedValue({ items: [{ id: '1', title: 'Listing 1' }] }),
  };

  const lookupsData = {
    propertyTypes: vi.fn().mockResolvedValue([{ id: 't1', code: 'house', name: 'House' }]),
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        SearchStore,
        { provide: ListingsDataService, useValue: listingsData },
        { provide: LookupsDataService, useValue: lookupsData },
        {
          provide: TranslocoService,
          useValue: { translate: (key: string) => key, setActiveLang: () => undefined },
        },
      ],
    });
    store = TestBed.inject(SearchStore);
  });

  it('loads property types during init', async () => {
    await store.init();
    expect(store.propertyTypes()).toEqual([{ id: 't1', code: 'house', name: 'House' }]);
  });

  it('searches and stores results', async () => {
    const criteria = { location: 'Kathmandu', minPrice: 1000 };
    await store.search(criteria);
    expect(listingsData.search).toHaveBeenCalledWith(criteria);
    expect(store.results()).toEqual([{ id: '1', title: 'Listing 1' }]);
    expect(store.criteria()).toEqual(criteria);
    expect(store.loading()).toBe(false);
    expect(store.error()).toBeNull();
  });

  it('stores an error when search fails', async () => {
    listingsData.search.mockRejectedValueOnce(new Error('boom'));
    await store.search({});
    expect(store.results()).toEqual([]);
    expect(store.error()).toBe('boom');
    expect(store.loading()).toBe(false);
  });
});
