import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { inject, Injectable } from '@angular/core';
import { Api } from '../../api/api';
import { RuntimeConfigService } from '../config/runtime-config.service';
import { v1ListingsGet$Json } from '../../api/fn/listings/v-1-listings-get-json';
import { v1ListingsIdGet$Json } from '../../api/fn/listings/v-1-listings-id-get-json';
import { v1ListingsMineGet$Json } from '../../api/fn/listings/v-1-listings-mine-get-json';
import { v1ListingsIdImagesImageIdDelete } from '../../api/fn/images/v-1-listings-id-images-image-id-delete';
import { v1ListingsIdImagesPost$Json } from '../../api/fn/images/v-1-listings-id-images-post-json';
import { v1ListingsMineIdGet$Json } from '../../api/fn/listings/v-1-listings-mine-id-get-json';
import type {
  CreateListingRequest,
  ListingDetailDto,
  ListingSummaryDto,
  ListingImageDto,
  OwnerListingDto,
  PagedResultOfListingSummaryDto,
  UpdateListingRequest,
} from '../../api/models';

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
  private readonly http = inject(HttpClient);
  private readonly config = inject(RuntimeConfigService);

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

  private baseUrl(): string {
    return `${this.config.value().apiBaseUrl}/v1/listings`;
  }

  create(request: CreateListingRequest, idempotencyKey?: string): Promise<OwnerListingDto> {
    const headers = idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : undefined;
    return firstValueFrom(
      this.http.post<OwnerListingDto>(this.baseUrl(), request, headers ? { headers } : {}),
    );
  }

  update(id: string, request: UpdateListingRequest, ifMatch?: string): Promise<OwnerListingDto> {
    const headers = ifMatch ? { 'If-Match': ifMatch } : undefined;
    return firstValueFrom(
      this.http.put<OwnerListingDto>(
        `${this.baseUrl()}/${id}`,
        request,
        headers ? { headers } : {},
      ),
    );
  }

  delete(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.baseUrl()}/${id}`));
  }

  publish(id: string, version?: string | null): Promise<OwnerListingDto> {
    return this.mutateStatus(id, 'publish', version);
  }

  markSold(id: string, version?: string | null): Promise<OwnerListingDto> {
    return this.mutateStatus(id, 'sold', version);
  }

  archive(id: string, version?: string | null): Promise<OwnerListingDto> {
    return this.mutateStatus(id, 'archive', version);
  }

  private mutateStatus(
    id: string,
    action: 'publish' | 'sold' | 'archive',
    version?: string | null,
  ): Promise<OwnerListingDto> {
    const headers = version ? { 'If-Match': version } : undefined;
    return firstValueFrom(
      this.http.post<OwnerListingDto>(
        `${this.baseUrl()}/${id}/${action}`,
        {},
        headers ? { headers } : {},
      ),
    );
  }

  uploadImage(id: string, file: File): Promise<ListingImageDto> {
    return this.api.invoke(v1ListingsIdImagesPost$Json, { id, body: { file } });
  }

  deleteImage(id: string, imageId: string): Promise<void> {
    return this.api.invoke(v1ListingsIdImagesImageIdDelete, { id, imageId });
  }
}
