import { ChangeDetectionStrategy, Component, ElementRef, inject, signal } from '@angular/core';
import { focusFirstInvalid } from '../../../shared/forms/focus-first-invalid';
import { ConfirmService } from '../../../shared/ui/confirm.service';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminCommissionRatesDataService } from '../../../core/data/admin-commission-rates-data.service';
import { ApiError } from '../../../core/http/api-error';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ErrorStateComponent } from '../../../shared/ui/error-state.component';
import { LoadingStateComponent } from '../../../shared/ui/loading-state.component';
import { HbPricePipe } from '../../../shared/pipes/hb-price.pipe';
import { HbNumberPipe } from '../../../shared/pipes/hb-number.pipe';
import { HbDateTimePipe } from '../../../shared/pipes/hb-date-time.pipe';
import type { CommissionRateDto, CommissionRateRequest } from '../../../api/models';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

@Component({
  selector: 'hb-commission-rates-admin-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    LoadingStateComponent,
    ErrorStateComponent,
    TranslocoPipe,
    HbPricePipe,
    HbNumberPipe,
    HbDateTimePipe,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatCheckboxModule,
    MatSlideToggleModule,
    MatTableModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
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
        <mat-card appearance="outlined" class="table-card">
          <mat-card-content>
            <div class="table-scroll">
              <table mat-table [dataSource]="rates()" class="rates-table">
                <ng-container matColumnDef="min">
                  <th mat-header-cell *matHeaderCellDef>{{ 'admin.min' | transloco }}</th>
                  <td mat-cell *matCellDef="let rate">
                    {{ rate.minPrice | hbPrice }} ({{
                      rate.minInclusive
                        ? ('admin.inclusive' | transloco)
                        : ('admin.exclusive' | transloco)
                    }})
                  </td>
                </ng-container>
                <ng-container matColumnDef="max">
                  <th mat-header-cell *matHeaderCellDef>{{ 'admin.max' | transloco }}</th>
                  <td mat-cell *matCellDef="let rate">
                    @if (rate.maxPrice !== null && rate.maxPrice !== undefined) {
                      {{ rate.maxPrice | hbPrice }} ({{
                        rate.maxInclusive
                          ? ('admin.inclusive' | transloco)
                          : ('admin.exclusive' | transloco)
                      }})
                    } @else {
                      {{ 'admin.open' | transloco }}
                    }
                  </td>
                </ng-container>
                <ng-container matColumnDef="rate">
                  <th mat-header-cell *matHeaderCellDef>{{ 'admin.ratePercent' | transloco }}</th>
                  <td mat-cell *matCellDef="let rate">
                    {{ rate.ratePercent | hbNumber: percentOptions }}%
                  </td>
                </ng-container>
                <ng-container matColumnDef="effective">
                  <th mat-header-cell *matHeaderCellDef>{{ 'admin.effectiveFrom' | transloco }}</th>
                  <td mat-cell *matCellDef="let rate">{{ rate.effectiveFrom | hbDateTime }}</td>
                </ng-container>
                <ng-container matColumnDef="active">
                  <th mat-header-cell *matHeaderCellDef>{{ 'admin.active' | transloco }}</th>
                  <td mat-cell *matCellDef="let rate">
                    {{ rate.isActive ? ('admin.yes' | transloco) : ('admin.no' | transloco) }}
                  </td>
                </ng-container>
                <ng-container matColumnDef="actions">
                  <th mat-header-cell *matHeaderCellDef></th>
                  <td mat-cell *matCellDef="let rate">
                    <button mat-stroked-button type="button" (click)="edit(rate)">
                      {{ 'admin.edit' | transloco }}
                    </button>
                  </td>
                </ng-container>

                <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
                <tr mat-row *matRowDef="let row; columns: displayedColumns"></tr>
              </table>
            </div>
          </mat-card-content>
        </mat-card>

        <mat-card appearance="outlined" class="rate-card">
          <mat-card-header>
            <mat-card-title>
              {{ editingId() ? ('admin.edit' | transloco) : ('admin.newRate' | transloco) }}
            </mat-card-title>
          </mat-card-header>
          <mat-card-content>
            @if (actionError()) {
              <p class="action-error" role="alert">{{ actionError() }}</p>
            }
            <form [formGroup]="form" (ngSubmit)="submit()" class="rate-form">
              <mat-form-field appearance="outline">
                <mat-label>{{ 'admin.minPrice' | transloco }}</mat-label>
                <input matInput type="number" formControlName="minPrice" />
              </mat-form-field>

              <mat-slide-toggle formControlName="minInclusive" color="primary">
                {{ 'admin.minInclusive' | transloco }}
              </mat-slide-toggle>

              <mat-form-field appearance="outline">
                <mat-label>{{ 'admin.maxPrice' | transloco }}</mat-label>
                <input matInput type="number" formControlName="maxPrice" />
              </mat-form-field>

              <mat-slide-toggle formControlName="maxInclusive" color="primary">
                {{ 'admin.maxInclusive' | transloco }}
              </mat-slide-toggle>

              <mat-form-field appearance="outline">
                <mat-label>{{ 'admin.ratePercent' | transloco }}</mat-label>
                <input matInput type="number" formControlName="ratePercent" />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{ 'admin.effectiveFrom' | transloco }}</mat-label>
                <input matInput type="datetime-local" formControlName="effectiveFrom" />
              </mat-form-field>

              <mat-slide-toggle formControlName="isActive" color="primary">
                {{ 'admin.active' | transloco }}
              </mat-slide-toggle>

              <div class="actions">
                @if (editingId()) {
                  <button mat-button type="button" (click)="cancelEdit()">
                    {{ 'common.cancel' | transloco }}
                  </button>
                }
                <button mat-raised-button color="primary" type="submit" [disabled]="saving()">
                  @if (saving()) {
                    <mat-spinner diameter="20" />
                  }
                  {{ saving() ? ('admin.saving' | transloco) : ('admin.save' | transloco) }}
                </button>
              </div>
            </form>
          </mat-card-content>
        </mat-card>
      }
    </section>
  `,
  styles: `
    .admin-page {
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
    }
    .table-scroll {
      overflow-x: auto;
    }
    .rates-table {
      width: 100%;
    }
    .rate-card {
      max-inline-size: 32rem;
    }
    .rate-form {
      display: grid;
      gap: 1rem;
    }
    .actions {
      display: flex;
      justify-content: flex-end;
    }
    .action-error {
      color: var(--mat-sys-error, #b3261e);
      margin: 0;
    }
  `,
})
export class CommissionRatesAdminPageComponent {
  private readonly data = inject(AdminCommissionRatesDataService);
  private readonly transloco = inject(TranslocoService);
  private readonly snack = inject(MatSnackBar);
  private readonly confirmService = inject(ConfirmService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly rates = signal<CommissionRateDto[]>([]);
  readonly editingId = signal<string | null>(null);
  readonly editingVersion = signal<string | null>(null);
  /** Failure of the last save; shown inline so the form and its field errors stay visible. */
  readonly actionError = signal<string | null>(null);

  readonly percentOptions: Intl.NumberFormatOptions = { maximumFractionDigits: 2 };
  readonly displayedColumns = ['min', 'max', 'rate', 'effective', 'active', 'actions'];

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

  /**
   * 412 means another admin changed the rate first. Offer a reload; on agreement the rate is
   * reloaded with its current version, and unsaved edits are replaced.
   */
  private async offerReload(id: string | null): Promise<void> {
    const reload = await this.confirmService.confirm({
      message: this.transloco.translate('admin.staleReload'),
      confirmLabel: this.transloco.translate('common.reload'),
    });
    if (!reload) {
      return;
    }
    await this.load();
    const fresh = id ? this.rates().find((rate) => rate.id === id) : undefined;
    if (fresh) {
      this.edit(fresh);
    } else {
      this.cancelEdit();
    }
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.editingVersion.set(null);
    this.form.reset({
      minInclusive: true,
      maxInclusive: false,
      isActive: true,
    });
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
      effectiveFrom:
        typeof rate.effectiveFrom === 'string' ? this.toLocalInputValue(rate.effectiveFrom) : '',
      isActive: rate.isActive ?? true,
    });
  }

  /**
   * Converts a stored UTC instant to the `YYYY-MM-DDTHH:mm` local value a datetime-local
   * input expects, so a round-trip through submit() keeps the same instant.
   */
  private toLocalInputValue(isoUtc: string): string {
    const date = new Date(isoUtc);
    if (Number.isNaN(date.getTime())) {
      return '';
    }
    const pad = (n: number): string => String(n).padStart(2, '0');
    return (
      `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
      `T${pad(date.getHours())}:${pad(date.getMinutes())}`
    );
  }

  async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      focusFirstInvalid(this.host.nativeElement);
      return;
    }
    this.saving.set(true);
    this.actionError.set(null);
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
      this.snack.open(this.transloco.translate('common.saved'), undefined, { duration: 3000 });
    } catch (error) {
      if (error instanceof ApiError && error.status === 412) {
        this.saving.set(false);
        await this.offerReload(id);
        return;
      }
      if (error instanceof ApiError && error.fieldErrors) {
        for (const [field, messages] of Object.entries(error.fieldErrors)) {
          const control = this.form.get(field);
          const first = messages?.[0];
          if (control && first) {
            control.setErrors({ server: first.message });
            control.markAsTouched();
          }
        }
      }
      this.actionError.set(
        error instanceof Error ? error.message : this.transloco.translate('errors.saveFailed'),
      );
    } finally {
      this.saving.set(false);
    }
  }
}
