import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { inject, Injectable } from '@angular/core';
import { Api } from '../../api/api';
import { v1AdminCommissionRatesGet$Json } from '../../api/fn/admin-commission-rates/v-1-admin-commission-rates-get-json';
import { v1AdminCommissionRatesPost$Json } from '../../api/fn/admin-commission-rates/v-1-admin-commission-rates-post-json';
import { RuntimeConfigService } from '../config/runtime-config.service';
import { IdempotencyKeyScope } from '../http/idempotency-key';
import type { CommissionRateDto, CommissionRateRequest } from '../../api/models';

@Injectable({ providedIn: 'root' })
export class AdminCommissionRatesDataService {
  private readonly api = inject(Api);
  private readonly http = inject(HttpClient);
  private readonly config = inject(RuntimeConfigService);
  private readonly createKeys = new IdempotencyKeyScope();

  list(): Promise<CommissionRateDto[]> {
    return this.api.invoke(v1AdminCommissionRatesGet$Json, {});
  }

  /** Creates a tier. A retry of the same unchanged request reuses its Idempotency-Key (plan 4.8). */
  async create(request: CommissionRateRequest): Promise<CommissionRateDto> {
    const created = await this.api.invoke(v1AdminCommissionRatesPost$Json, {
      body: request,
      'Idempotency-Key': this.createKeys.keyFor(JSON.stringify(request)),
    });
    this.createKeys.reset();
    return created;
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
