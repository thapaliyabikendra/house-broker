import { inject, Injectable } from '@angular/core';
import { Api } from '../../api/api';
import { v1LookupsPropertyTypesGet$Json } from '../../api/fn/lookups/v-1-lookups-property-types-get-json';
import { v1LookupsFeaturesGet$Json } from '../../api/fn/lookups/v-1-lookups-features-get-json';
import type { FeatureDto, PropertyTypeDto } from '../../api/models';

@Injectable({ providedIn: 'root' })
export class LookupsDataService {
  private readonly api = inject(Api);

  propertyTypes(): Promise<PropertyTypeDto[]> {
    return this.api.invoke(v1LookupsPropertyTypesGet$Json, {});
  }

  features(): Promise<FeatureDto[]> {
    return this.api.invoke(v1LookupsFeaturesGet$Json, {});
  }
}
