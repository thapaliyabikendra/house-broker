import { ChangeDetectionStrategy, Component, ElementRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { focusFirstInvalid } from '../../../shared/forms/focus-first-invalid';
import { ConfirmService } from '../../../shared/ui/confirm.service';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminCommissionRatesDataService } from '../../../core/data/admin-commission-rates-data.service';
import { CommissionRateEditorDialogComponent } from '../components/commission-rate-editor-dialog.component';
import { ApiError } from '../../../core/http/api-error';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ErrorStateComponent } from '../../../shared/ui/error-state.component';
import { LoadingStateComponent } from '../../../shared/ui/loading-state.component';
import { HbPricePipe } from '../../../shared/pipes/hb-price.pipe';
import { HbNumberPipe } from '../../../shared/pipes/hb-number.pipe';
import { HbDatePipe } from '../../../shared/pipes/hb-date.pipe';
import { HbDateTimePipe } from '../../../shared/pipes/hb-date-time.pipe';
import type { CommissionRateDto, CommissionRateRequest } from '../../../api/models';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

interface BandPreview {
  min: number | null;
  minInclusive: boolean;
  max: number | null;
  maxInclusive: boolean;
  rate: number | null;
}

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
    HbDatePipe,
    HbDateTimePipe,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatCheckboxModule,
    MatSlideToggleModule,
    MatTableModule,
    MatDialogModule,
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
        <div class="admin-layout">
          <mat-card appearance="outlined" class="table-card">
            <mat-card-content>
              <div class="table-toolbar">
                <button mat-raised-button type="button" (click)="startNew()">
                  {{ 'admin.newRate' | transloco }}
                </button>
              </div>
              <div class="table-scroll">
                <table mat-table [dataSource]="rates()" class="rates-table">
                  <ng-container matColumnDef="min">
                    <th mat-header-cell *matHeaderCellDef>{{ 'admin.min' | transloco }}</th>
                    <td mat-cell *matCellDef="let rate">
                      <div class="bound-symbol">{{ rate.minInclusive ? '≥' : '>' }}</div>
                      <div class="bound-amount">{{ rate.minPrice | hbPrice }}</div>
                    </td>
                  </ng-container>
                  <ng-container matColumnDef="max">
                    <th mat-header-cell *matHeaderCellDef>{{ 'admin.max' | transloco }}</th>
                    <td mat-cell *matCellDef="let rate">
                      @if (rate.maxPrice !== null && rate.maxPrice !== undefined) {
                        <div class="bound-symbol">{{ rate.maxInclusive ? '≤' : '<' }}</div>
                        <div class="bound-amount">{{ rate.maxPrice | hbPrice }}</div>
                      } @else {
                        {{ 'admin.andAbove' | transloco }}
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
                    <th mat-header-cell *matHeaderCellDef>
                      {{ 'admin.effectiveFrom' | transloco }}
                    </th>
                    <td mat-cell *matCellDef="let rate">
                      <div class="effective-date">{{ rate.effectiveFrom | hbDate }}</div>
                      <div class="effective-time">
                        {{ rate.effectiveFrom | hbDateTime: timeOnlyOptions }}
                      </div>
                    </td>
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
        </div>
      }
    </section>
  `,
  styles: `
    .admin-page {
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
      max-inline-size: 110rem;
      margin-inline: auto;
    }
    .table-scroll {
      overflow-x: hidden;
    }
    .rates-table {
      width: 100%;
      border-collapse: separate;
    }
    .rates-table th,
    .rates-table td {
      padding-inline: 1rem;
      padding-block: 0.75rem;
      font-size: 0.95rem;
    }
    .effective-date {
      font-weight: 600;
    }
    .effective-time {
      display: block;
      font-size: 0.85rem;
      color: var(--mat-sys-on-surface-variant);
    }
    .bound-symbol {
      font-weight: 700;
      line-height: 1;
    }
    .bound-amount {
      display: block;
      white-space: nowrap;
    }
    .rates-table .mat-column-min,
    .rates-table .mat-column-max {
      min-inline-size: 11rem;
    }
    .rates-table .mat-column-effective {
      min-inline-size: 9rem;
    }
    .rates-table .mat-column-rate,
    .rates-table .mat-column-active {
      min-inline-size: 7rem;
    }
    .rates-table th {
      font-weight: 700;
    }
    .admin-layout {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      gap: 1.5rem;
      align-items: start;
    }
    .table-scroll {
      overflow-x: hidden;
    }
    .rates-table {
      table-layout: fixed;
    }
    .rates-table th,
    .rates-table td {
      white-space: normal;
      overflow-wrap: anywhere;
    }
    .table-toolbar {
      display: flex;
      justify-content: flex-end;
      margin-block-end: 0.75rem;
    }
    .rate-card {
      position: sticky;
      inset-block-start: 1rem;
    }
    .note {
      margin: 0 0 1rem;
      color: var(--mat-sys-on-surface-variant);
      font-size: 0.9rem;
    }
    .band-preview {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      margin: 0;
      padding: 0.5rem 0.75rem;
      border-radius: 8px;
      background: var(--mat-sys-surface-container-low);
    }
    .band-label {
      font-weight: 500;
    }
    @media (max-width: 900px) {
      .admin-layout {
        grid-template-columns: minmax(0, 1fr);
      }
      .rate-card {
        position: static;
      }
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
  private readonly dialog = inject(MatDialog, { optional: true });
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
  readonly timeOnlyOptions: Intl.DateTimeFormatOptions = {
    year: undefined,
    month: undefined,
    day: undefined,
    hour: 'numeric',
    minute: '2-digit',
  };
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

  /** Live summary of the band being entered, so overlaps and gaps are visible before saving. */
  readonly preview = signal<BandPreview | null>(null);

  constructor() {
    this.form.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => this.updatePreview());
    void this.load();
  }

  private updatePreview(): void {
    const value = this.form.getRawValue();
    this.preview.set({
      min: value.minPrice,
      minInclusive: value.minInclusive,
      max: value.maxPrice,
      maxInclusive: value.maxInclusive,
      rate: value.ratePercent,
    });
  }

  /** Opens the create dialog; reloads the list after a save. */
  startNew(): void {
    this.cancelEdit();
    if (!this.dialog) {
      return;
    }
    this.dialog
      .open(CommissionRateEditorDialogComponent, {
        data: { rate: null },
        width: '48rem',
        maxWidth: '95vw',
      })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) {
          void this.load();
        }
      });
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

    if (this.dialog) {
      this.dialog
        .open(CommissionRateEditorDialogComponent, {
          data: { rate },
          width: '40rem',
          maxWidth: '95vw',
        })
        .afterClosed()
        .subscribe((saved) => {
          if (saved) {
            void this.load();
          }
        });
    }
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

  /** First server-provided field message, if the failure carries field errors. */
  private firstFieldMessage(error: unknown): string | null {
    if (!(error instanceof ApiError) || !error.fieldErrors) {
      return null;
    }
    for (const messages of Object.values(error.fieldErrors)) {
      const first = messages?.[0];
      if (first?.message) {
        return first.message;
      }
    }
    return null;
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
        this.firstFieldMessage(error) ??
          (error instanceof Error ? error.message : this.transloco.translate('errors.saveFailed')),
      );
    } finally {
      this.saving.set(false);
    }
  }
}
