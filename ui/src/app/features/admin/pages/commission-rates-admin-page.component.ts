import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatTableModule } from '@angular/material/table';
import type { CommissionRateDto } from '../../../api/models';
import { AdminCommissionRatesDataService } from '../../../core/data/admin-commission-rates-data.service';
import { HbDatePipe } from '../../../shared/pipes/hb-date.pipe';
import { HbDateTimePipe } from '../../../shared/pipes/hb-date-time.pipe';
import { HbNumberPipe } from '../../../shared/pipes/hb-number.pipe';
import { HbPricePipe } from '../../../shared/pipes/hb-price.pipe';
import { ConfirmService } from '../../../shared/ui/confirm.service';
import { ErrorStateComponent } from '../../../shared/ui/error-state.component';
import { LoadingStateComponent } from '../../../shared/ui/loading-state.component';
import { CommissionRateEditorDialogComponent } from '../components/commission-rate-editor-dialog.component';

/** Both create and edit open the editor at the same width, so the dialog does not jump. */
const EDITOR_DIALOG_WIDTH = '40rem';

@Component({
  selector: 'hb-commission-rates-admin-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    LoadingStateComponent,
    ErrorStateComponent,
    TranslocoPipe,
    HbPricePipe,
    HbNumberPipe,
    HbDatePipe,
    HbDateTimePipe,
    MatCardModule,
    MatButtonModule,
    MatTableModule,
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
                <button
                  mat-raised-button
                  type="button"
                  (click)="startNew()"
                  [disabled]="busy() !== null"
                >
                  {{ 'admin.newRate' | transloco }}
                </button>
              </div>
              <div class="table-scroll">
                <table mat-table [dataSource]="rates()" class="rates-table">
                  <ng-container matColumnDef="min">
                    <th mat-header-cell *matHeaderCellDef>{{ 'admin.min' | transloco }}</th>
                    <td mat-cell *matCellDef="let rate">
                      @if (rate.minPrice !== null && rate.minPrice !== undefined) {
                        <div class="bound-symbol">{{ rate.minInclusive ? '≥' : '>' }}</div>
                        <div class="bound-amount">{{ rate.minPrice | hbPrice }}</div>
                      } @else {
                        <div class="bound-amount">{{ 'admin.notApplicable' | transloco }}</div>
                      }
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
                  <ng-container matColumnDef="status">
                    <th mat-header-cell *matHeaderCellDef>{{ 'admin.status' | transloco }}</th>
                    <td mat-cell *matCellDef="let rate">
                      {{ statusLabel(rate) }}
                    </td>
                  </ng-container>
                  <ng-container matColumnDef="actions">
                    <th mat-header-cell *matHeaderCellDef></th>
                    <td mat-cell *matCellDef="let rate">
                      <button
                        mat-stroked-button
                        type="button"
                        (click)="edit(rate)"
                        [disabled]="busy() === rate.id"
                      >
                        {{ 'admin.edit' | transloco }}
                      </button>
                      @if (rate.isActive) {
                        <button
                          mat-stroked-button
                          color="warn"
                          type="button"
                          (click)="retire(rate)"
                          [disabled]="busy() === rate.id"
                        >
                          {{ 'admin.retire' | transloco }}
                        </button>
                      } @else {
                        <button
                          mat-stroked-button
                          type="button"
                          (click)="reactivate(rate)"
                          [disabled]="busy() === rate.id"
                        >
                          {{ 'admin.reactivate' | transloco }}
                        </button>
                      }
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
      table-layout: fixed;
    }
    .rates-table th,
    .rates-table td {
      padding-inline: 1rem;
      padding-block: 0.75rem;
      font-size: 0.95rem;
      white-space: normal;
      overflow-wrap: anywhere;
    }
    .rates-table th {
      font-weight: 700;
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
    .rates-table .mat-column-status {
      min-inline-size: 7rem;
    }
    .admin-layout {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      gap: 1.5rem;
      align-items: start;
    }
    .table-toolbar {
      display: flex;
      justify-content: flex-end;
      margin-block-end: 0.75rem;
    }
  `,
})
export class CommissionRatesAdminPageComponent {
  private readonly data = inject(AdminCommissionRatesDataService);
  private readonly transloco = inject(TranslocoService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  private readonly confirm = inject(ConfirmService);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly rates = signal<CommissionRateDto[]>([]);
  readonly busy = signal<string | null>(null);

  readonly percentOptions: Intl.NumberFormatOptions = { maximumFractionDigits: 2 };
  readonly timeOnlyOptions: Intl.DateTimeFormatOptions = {
    year: undefined,
    month: undefined,
    day: undefined,
    hour: 'numeric',
    minute: '2-digit',
  };
  readonly displayedColumns = ['min', 'max', 'rate', 'effective', 'status', 'actions'];

  constructor() {
    void this.load();
  }

  /** Opens the create dialog; reloads the list after a save. */
  startNew(): void {
    this.openEditor(null);
  }

  /** Opens the edit dialog for one rate; reloads the list after a save. */
  edit(rate: CommissionRateDto): void {
    this.openEditor(rate);
  }

  async retire(rate: CommissionRateDto): Promise<void> {
    if (!rate.id) {
      return;
    }
    const discard = await this.confirm.confirm({
      message: this.transloco.translate('admin.retireConfirm'),
      confirmLabel: this.transloco.translate('admin.retire'),
      destructive: true,
    });
    if (!discard) {
      return;
    }
    this.busy.set(rate.id);
    try {
      await this.data.retire(rate.id, rate.version);
      this.snack.open(this.transloco.translate('admin.retiredNotice'), undefined, {
        duration: 3000,
      });
      await this.load();
    } catch (error) {
      const message =
        error instanceof Error ? error.message : this.transloco.translate('errors.deleteFailed');
      this.snack.open(message, undefined, { duration: 4000 });
    } finally {
      this.busy.set(null);
    }
  }

  async reactivate(rate: CommissionRateDto): Promise<void> {
    if (!rate.id) {
      return;
    }
    this.busy.set(rate.id);
    try {
      await this.data.reactivate(rate.id, rate.version);
      this.snack.open(this.transloco.translate('admin.reactivatedNotice'), undefined, {
        duration: 3000,
      });
      await this.load();
    } catch (error) {
      const message =
        error instanceof Error ? error.message : this.transloco.translate('errors.saveFailed');
      this.snack.open(message, undefined, { duration: 4000 });
    } finally {
      this.busy.set(null);
    }
  }

  statusLabel(rate: CommissionRateDto): string {
    if (!rate.isActive) {
      return this.transloco.translate('admin.statusRetired');
    }
    const effective = new Date(String(rate.effectiveFrom));
    if (!Number.isNaN(effective.getTime()) && effective.getTime() > Date.now()) {
      return this.transloco.translate('admin.statusScheduled');
    }
    return this.transloco.translate('admin.statusActive');
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

  private openEditor(rate: CommissionRateDto | null): void {
    this.dialog
      .open(CommissionRateEditorDialogComponent, {
        data: { rate },
        width: EDITOR_DIALOG_WIDTH,
        maxWidth: '95vw',
        disableClose: true,
      })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) {
          void this.load();
        }
      });
  }
}
