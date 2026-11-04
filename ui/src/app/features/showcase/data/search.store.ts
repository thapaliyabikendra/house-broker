import { inject, Injectable, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { ListingsDataService } from '../../../core/data/listings-data.service';
import type { ListingSearchCriteria } from '../../../core/data/listings-data.service';
import { LookupsDataService } from '../../../core/data/lookups-data.service';
import type {
  ListingSummaryDto,
  PagedResultOfListingSummaryDto,
  PropertyTypeDto,
} from '../../../api/models';

@Injectable()
export class SearchStore {
  private readonly listingsData = inject(ListingsDataService);
  private readonly lookupsData = inject(LookupsDataService);
  private readonly transloco = inject(TranslocoService);

  readonly propertyTypes = signal<PropertyTypeDto[]>([]);
  readonly results = signal<ListingSummaryDto[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly criteria = signal<ListingSearchCriteria>({});
  readonly page = signal<number | null>(null);
  readonly pageSize = signal<number | null>(null);
  readonly total = signal<number | null>(null);

  async init(): Promise<void> {
    try {
      this.propertyTypes.set(await this.lookupsData.propertyTypes());
    } catch {
      this.propertyTypes.set([]);
    }
  }

  async search(criteria: ListingSearchCriteria): Promise<void> {
    this.criteria.set(criteria);
    this.loading.set(true);
    this.error.set(null);
    try {
      const page = (await this.listingsData.search(criteria)) as PagedResultOfListingSummaryDto;
      this.results.set(page.items ?? []);
      this.page.set(page.page != null ? Number(page.page) : null);
      this.pageSize.set(page.pageSize != null ? Number(page.pageSize) : null);
      this.total.set(page.total != null ? Number(page.total) : null);
    } catch (error) {
      this.error.set(
        error instanceof Error ? error.message : this.transloco.translate('errors.searchFailed'),
      );
      this.results.set([]);
      this.total.set(null);
    } finally {
      this.loading.set(false);
    }
  }
}
