import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { inject, Injectable } from '@angular/core';
import { Api } from '../../api/api';
import { v1AdminCommissionRatesGet$Json } from '../../api/fn/admin-commission-rates/v-1-admin-commission-rates-get-json';
import { v1AdminCommissionRatesPost$Json } from '../../api/fn/admin-commission-rates/v-1-admin-commission-rates-post-json';
import { RuntimeConfigService } from '../config/runtime-config.service';
import type { CommissionRateDto, CommissionRateRequest } from '../../api/models';

@Injectable({ providedIn: 'root' })
export class AdminCommissionRatesDataService {
  private readonly api = inject(Api);
  private readonly http = inject(HttpClient);
  private readonly config = inject(RuntimeConfigService);

  list(): Promise<CommissionRateDto[]> {
    return this.api.invoke(v1AdminCommissionRatesGet$Json, {});
  }

  create(request: CommissionRateRequest): Promise<CommissionRateDto> {
    return this.api.invoke(v1AdminCommissionRatesPost$Json, { body: request });
  }

  update(
    id: string,
    request: CommissionRateRequest,
    version?: string | null,
  ): Promise<CommissionRateDto> {
    const headers = version ? { 'If-Match': version } : undefined;
    return firstValueFrom(
      this.http.put<CommissionRateDto>(
        `${this.config.value().apiBaseUrl}/v1/admin/commission-rates/${id}`,
        request,
        headers ? { headers } : {},
      ),
    );
  }
}
