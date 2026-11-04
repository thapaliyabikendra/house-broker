import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminCommissionRatesDataService } from '../../../core/data/admin-commission-rates-data.service';
import { ApiError } from '../../../core/http/api-error';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ErrorStateComponent } from '../../../shared/ui/error-state.component';
import { LoadingStateComponent } from '../../../shared/ui/loading-state.component';
import type { CommissionRateDto, CommissionRateRequest } from '../../../api/models';

@Component({
  selector: 'hb-commission-rates-admin-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, LoadingStateComponent, ErrorStateComponent, TranslocoPipe],
  template: `
    <section class="admin-page">
      <h1>{{ 'admin.title' | transloco }}</h1>

      @if (loading()) {
        <hb-loading-state [label]="'admin.loading' | transloco" />
      } @else if (error()) {
        <hb-error-state
          [title]="error() ?? ('common.error' | transloco)"
          (retry)="load()"
          [actionLabel]="'common.retry' | transloco"
        />
      } @else {
        <table class="rates-table">
          <thead>
            <tr>
              <th>{{ 'admin.min' | transloco }}</th>
              <th>{{ 'admin.max' | transloco }}</th>
              <th>{{ 'admin.ratePercent' | transloco }}</th>
              <th>{{ 'admin.effectiveFrom' | transloco }}</th>
              <th>{{ 'admin.active' | transloco }}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (rate of rates(); track rate.id) {
              <tr>
                <td>
                  {{ rate.minPrice }} ({{
                    rate.minInclusive
                      ? ('admin.inclusive' | transloco)
                      : ('admin.exclusive' | transloco)
                  }})
                </td>
                <td>
                  @if (rate.maxPrice !== null && rate.maxPrice !== undefined) {
                    {{ rate.maxPrice }} ({{
                      rate.maxInclusive
                        ? ('admin.inclusive' | transloco)
                        : ('admin.exclusive' | transloco)
                    }})
                  } @else {
                    {{ 'admin.open' | transloco }}
                  }
                </td>
                <td>{{ rate.ratePercent }}</td>
                <td>{{ rate.effectiveFrom }}</td>
                <td>{{ rate.isActive ? ('admin.yes' | transloco) : ('admin.no' | transloco) }}</td>
                <td>
                  <button type="button" (click)="edit(rate)">{{ 'admin.edit' | transloco }}</button>
                </td>
              </tr>
            }
          </tbody>
        </table>

        <form [formGroup]="form" (ngSubmit)="submit()" class="rate-form">
          <h2>{{ editingId() ? ('admin.edit' | transloco) : ('admin.newRate' | transloco) }}</h2>

          <label>
            {{ 'admin.minPrice' | transloco }}
            <input type="number" formControlName="minPrice" />
          </label>
          <label>
            {{ 'admin.minInclusive' | transloco }}
            <input type="checkbox" formControlName="minInclusive" />
          </label>
          <label>
            {{ 'admin.maxPrice' | transloco }}
            <input type="number" formControlName="maxPrice" />
          </label>
          <label>
            {{ 'admin.maxInclusive' | transloco }}
            <input type="checkbox" formControlName="maxInclusive" />
          </label>
          <label>
            {{ 'admin.ratePercent' | transloco }}
            <input type="number" formControlName="ratePercent" />
          </label>
          <label>
            {{ 'admin.effectiveFrom' | transloco }}
            <input type="datetime-local" formControlName="effectiveFrom" />
          </label>
          <label>
            {{ 'admin.active' | transloco }}
            <input type="checkbox" formControlName="isActive" />
          </label>

          <button type="submit" [disabled]="saving() || form.invalid">
            {{ saving() ? ('admin.saving' | transloco) : ('admin.save' | transloco) }}
          </button>
        </form>
      }
    </section>
  `,
  styles: `
    .admin-page {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .rate-form {
      display: grid;
      gap: 0.75rem;
      max-inline-size: 32rem;
    }
    label {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }
  `,
})
export class CommissionRatesAdminPageComponent {
  private readonly data = inject(AdminCommissionRatesDataService);
  private readonly transloco = inject(TranslocoService);

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly rates = signal<CommissionRateDto[]>([]);
  readonly editingId = signal<string | null>(null);
  readonly editingVersion = signal<string | null>(null);

  readonly form = new FormGroup({
    minPrice: new FormControl<number | null>(null, {
      validators: [Validators.required, Validators.min(0)],
    }),
    minInclusive: new FormControl<boolean>(true, { nonNullable: true }),
    maxPrice: new FormControl<number | null>(null),
    maxInclusive: new FormControl<boolean>(false, { nonNullable: true }),
    ratePercent: new FormControl<number | null>(null, {
      validators: [Validators.required, Validators.min(0)],
    }),
    effectiveFrom: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    isActive: new FormControl<boolean>(true, { nonNullable: true }),
  });

  constructor() {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.rates.set(await this.data.list());
    } catch (error) {
      this.error.set(
        error instanceof Error ? error.message : this.transloco.translate('errors.loadFailed'),
      );
    } finally {
      this.loading.set(false);
    }
  }

  edit(rate: CommissionRateDto): void {
    this.editingId.set(rate.id ?? null);
    this.editingVersion.set(rate.version ?? null);
    this.form.patchValue({
      minPrice: rate.minPrice != null ? Number(rate.minPrice) : null,
      minInclusive: rate.minInclusive ?? true,
      maxPrice: rate.maxPrice != null ? Number(rate.maxPrice) : null,
      maxInclusive: rate.maxInclusive ?? false,
      ratePercent: rate.ratePercent != null ? Number(rate.ratePercent) : null,
      effectiveFrom: typeof rate.effectiveFrom === 'string' ? rate.effectiveFrom.slice(0, 16) : '',
      isActive: rate.isActive ?? true,
    });
  }

  async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    const value = this.form.getRawValue();
    const request: CommissionRateRequest = {
      minPrice: value.minPrice ?? 0,
      minInclusive: value.minInclusive,
      maxPrice: value.maxPrice ?? null,
      maxInclusive: value.maxInclusive,
      ratePercent: value.ratePercent ?? 0,
      effectiveFrom: value.effectiveFrom ? new Date(value.effectiveFrom).toISOString() : undefined,
      isActive: value.isActive,
    };
    const id = this.editingId();
    try {
      if (id) {
        await this.data.update(id, request, this.editingVersion());
      } else {
        await this.data.create(request);
      }
      this.editingId.set(null);
      this.editingVersion.set(null);
      this.form.reset({
        minInclusive: true,
        maxInclusive: false,
        isActive: true,
      });
      await this.load();
    } catch (error) {
      if (error instanceof ApiError && error.fieldErrors) {
        for (const [field, messages] of Object.entries(error.fieldErrors)) {
          const control = this.form.get(field);
          if (control && messages?.length) {
            control.setErrors({ server: messages[0] });
          }
        }
      }
      this.error.set(
        error instanceof Error ? error.message : this.transloco.translate('errors.saveFailed'),
      );
    } finally {
      this.saving.set(false);
    }
  }
}
