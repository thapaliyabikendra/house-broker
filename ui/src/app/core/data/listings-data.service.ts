import { inject, Injectable } from '@angular/core';
import { Api } from '../../api/api';
import { v1ListingsGet$Json } from '../../api/fn/listings/v-1-listings-get-json';
import { v1ListingsIdGet$Json } from '../../api/fn/listings/v-1-listings-id-get-json';
import { v1ListingsMineGet$Json } from '../../api/fn/listings/v-1-listings-mine-get-json';
import { v1ListingsMineIdGet$Json } from '../../api/fn/listings/v-1-listings-mine-id-get-json';
import type { ListingDetailDto, ListingSummaryDto, OwnerListingDto, PagedResultOfListingSummaryDto } from '../../api/models';

export interface ListingSearchCriteria {
  location?: string;
  minPrice?: number;
  maxPrice?: number;
  propertyTypeId?: string;
  bedrooms?: number;
  featureIds?: string[];
  sort?: string;
  page?: number;
  pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class ListingsDataService {
  private readonly api = inject(Api);

  search(criteria: ListingSearchCriteria = {}): Promise<PagedResultOfListingSummaryDto> {
    return this.api.invoke(v1ListingsGet$Json, {
      Location: criteria.location,
      MinPrice: criteria.minPrice,
      MaxPrice: criteria.maxPrice,
      PropertyTypeId: criteria.propertyTypeId,
      Bedrooms: criteria.bedrooms,
      FeatureIds: criteria.featureIds,
      Sort: criteria.sort,
      Page: criteria.page,
      PageSize: criteria.pageSize,
    });
  }

  getById(id: string): Promise<ListingDetailDto> {
    return this.api.invoke(v1ListingsIdGet$Json, { id });
  }

  getMine(): Promise<ListingSummaryDto[]> {
    return this.api.invoke(v1ListingsMineGet$Json, {});
  }

  getMineById(id: string): Promise<OwnerListingDto> {
    return this.api.invoke(v1ListingsMineIdGet$Json, { id });
  }
}
