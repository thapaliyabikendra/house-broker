import { inject, Injectable } from '@angular/core';
import { Api } from '../../api/api';
import { v1AdminCommissionRatesGet$Json } from '../../api/fn/admin-commission-rates/v-1-admin-commission-rates-get-json';
import { v1AdminCommissionRatesIdPut$Json } from '../../api/fn/admin-commission-rates/v-1-admin-commission-rates-id-put-json';
import { v1AdminCommissionRatesPost$Json } from '../../api/fn/admin-commission-rates/v-1-admin-commission-rates-post-json';
import type { CommissionRateDto, CommissionRateRequest } from '../../api/models';

@Injectable({ providedIn: 'root' })
export class AdminCommissionRatesDataService {
  private readonly api = inject(Api);

  list(): Promise<CommissionRateDto[]> {
    return this.api.invoke(v1AdminCommissionRatesGet$Json, {});
  }

  create(request: CommissionRateRequest): Promise<CommissionRateDto> {
    return this.api.invoke(v1AdminCommissionRatesPost$Json, { body: request });
  }

  update(id: string, request: CommissionRateRequest): Promise<CommissionRateDto> {
    return this.api.invoke(v1AdminCommissionRatesIdPut$Json, { id, body: request });
  }
}
