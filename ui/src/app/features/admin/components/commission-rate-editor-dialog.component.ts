import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  inject,
  signal,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { filter } from 'rxjs';
import type { CommissionRateDto, CommissionRateRequest } from '../../../api/models';
import { AdminCommissionRatesDataService } from '../../../core/data/admin-commission-rates-data.service';
import { ApiError } from '../../../core/http/api-error';
import { applyServerErrors } from '../../../shared/forms/apply-server-errors';
import { focusFirstInvalid } from '../../../shared/forms/focus-first-invalid';
import { ConfirmService } from '../../../shared/ui/confirm.service';

export interface CommissionRateEditorDialogData {
  rate: CommissionRateDto | null;
}

/** Controls rendered with an inline error slot. Server errors for any other field go to the banner. */
const INLINE_FIELDS = ['maxPrice', 'maxInclusive', 'ratePercent', 'effectiveFrom'] as const;

/** Local `YYYY-MM-DDTHH:mm:ss` value for a datetime-local input, in the browser's zone. */
function toLocalInputValue(isoUtc: string): string {
  const date = new Date(isoUtc);
  if (Number.isNaN(date.getTime())) {
    return '';
  }
  const pad = (n: number): string => String(n).padStart(2, '0');
  return (
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
    `T${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`
  );
}

function numberOrNull(value: number | string | null | undefined): number | null {
  return value != null ? Number(value) : null;
}

/**
 * Create or edit a commission band. A band stores only its upper bound; the lower bound is derived
 * from the previous band, so there is no min price or min-inclusive input. An empty max price makes
 * the band the open-ended top band. Bands are always created active and retired from the list.
 * Closes with `true` after a save (or after the admin accepts a reload on a stale version) and
 * `false` on cancel. Opened with `disableClose`, so Escape and the backdrop reach requestClose(),
 * which asks before discarding edits.
 */
@Component({
  selector: 'hb-commission-rate-editor-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    TranslocoPipe,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSlideToggleModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <h2 mat-dialog-title>
      {{ data.rate ? ('admin.edit' | transloco) : ('admin.newRate' | transloco) }}
    </h2>
    <mat-dialog-content class="dialog-body">
      @if (banner().length > 0) {
        <div class="action-error" role="alert">
          @for (message of banner(); track $index) {
            <p>{{ message }}</p>
          }
          @if (relatedHint()) {
            <p class="hint">{{ 'admin.relatedTiers' | transloco }}</p>
          }
        </div>
      }
      <form id="rate-form" [formGroup]="form" (ngSubmit)="submit()" class="rate-form">
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ 'admin.maxPrice' | transloco }}</mat-label>
          <input matInput type="number" formControlName="maxPrice" />
          <mat-hint>{{ 'admin.maxPriceHint' | transloco }}</mat-hint>
          @if (form.controls.maxPrice.invalid && form.controls.maxPrice.touched) {
            <mat-error>{{
              form.controls.maxPrice.errors?.['server'] ?? ('admin.maxPriceError' | transloco)
            }}</mat-error>
          }
        </mat-form-field>

        <div class="toggle-field">
          <mat-slide-toggle
            formControlName="maxInclusive"
            color="primary"
            [disabled]="openEnded()"
            [aria-describedby]="toggleErrorId('maxInclusive')"
          >
            {{ 'admin.maxInclusive' | transloco }}
          </mat-slide-toggle>
          @if (serverError('maxInclusive'); as message) {
            <p id="maxInclusive-error" class="field-error">{{ message }}</p>
          }
        </div>

        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ 'admin.ratePercent' | transloco }}</mat-label>
          <input matInput type="number" formControlName="ratePercent" />
          <span matTextSuffix>%</span>
          @if (form.controls.ratePercent.invalid && form.controls.ratePercent.touched) {
            <mat-error>{{
              form.controls.ratePercent.errors?.['server'] ?? ('admin.ratePercentError' | transloco)
            }}</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ 'admin.effectiveFrom' | transloco }}</mat-label>
          <input matInput type="datetime-local" step="1" formControlName="effectiveFrom" />
          @if (form.controls.effectiveFrom.invalid && form.controls.effectiveFrom.touched) {
            <mat-error>{{ form.controls.effectiveFrom.errors?.['server'] }}</mat-error>
          }
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="requestClose()">
        {{ 'common.cancel' | transloco }}
      </button>
      <button
        mat-raised-button
        color="primary"
        type="submit"
        form="rate-form"
        [disabled]="saving()"
      >
        @if (saving()) {
          <mat-spinner diameter="20" />
        }
        {{ saving() ? ('admin.saving' | transloco) : ('admin.save' | transloco) }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .dialog-body {
      min-inline-size: 24rem;
    }
    .rate-form {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 1rem 1.25rem;
      padding-block-start: 0.5rem;
    }
    .rate-form mat-form-field {
      width: 100%;
    }
    .toggle-field {
      display: flex;
      flex-direction: column;
      justify-content: center;
      gap: 0.25rem;
    }
    .toggle-field mat-slide-toggle {
      align-self: flex-start;
      padding-block: 0.5rem;
    }
    .rate-form mat-form-field:has(input[type='datetime-local']) {
      grid-column: 1 / -1;
    }
    .field-error {
      margin: 0;
      color: var(--mat-sys-error, #b3261e);
      font-size: 0.85rem;
    }
    .action-error {
      color: var(--mat-sys-error, #b3261e);
      margin: 0 0 0.75rem;
    }
    .action-error p {
      margin: 0 0 0.25rem;
    }
    .action-error .hint {
      color: var(--mat-sys-on-surface-variant);
    }
    @media (max-width: 680px) {
      .rate-form {
        grid-template-columns: minmax(0, 1fr);
      }
    }
  `,
})
export class CommissionRateEditorDialogComponent {
  protected readonly data = inject<CommissionRateEditorDialogData>(MAT_DIALOG_DATA);
  private readonly dataService = inject(AdminCommissionRatesDataService);
  private readonly transloco = inject(TranslocoService);
  private readonly snack = inject(MatSnackBar);
  private readonly dialogRef = inject(MatDialogRef<CommissionRateEditorDialogComponent, boolean>);
  private readonly confirmService = inject(ConfirmService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly saving = signal(false);
  /** Messages that could not be shown under a control: the banner lists them. */
  readonly banner = signal<string[]>([]);
  /** Set when a field-less conflict names other records that must change too. */
  readonly relatedHint = signal(false);
  /** True when the band is the open-ended top band: no max price, so max-inclusive does not apply. */
  readonly openEnded = signal(this.data.rate?.maxPrice == null);
  private readonly editingVersion = this.data.rate?.version ?? null;
  /** The stored instant and its local form, so an untouched effective date is sent unchanged. */
  private readonly originalEffectiveFrom = this.data.rate?.effectiveFrom
    ? String(this.data.rate.effectiveFrom)
    : null;
  private readonly originalEffectiveLocal = this.originalEffectiveFrom
    ? toLocalInputValue(this.originalEffectiveFrom)
    : '';

  readonly form = new FormGroup({
    maxPrice: new FormControl<number | null>(numberOrNull(this.data.rate?.maxPrice), {
      validators: [Validators.min(0.01)],
    }),
    maxInclusive: new FormControl<boolean>(this.data.rate?.maxInclusive ?? false, {
      nonNullable: true,
    }),
    ratePercent: new FormControl<number | null>(numberOrNull(this.data.rate?.ratePercent), {
      validators: [Validators.required, Validators.min(0.0001), Validators.max(100)],
    }),
    effectiveFrom: new FormControl<string>(this.originalEffectiveLocal, { nonNullable: true }),
  });

  constructor() {
    this.dialogRef
      .keydownEvents()
      .pipe(
        filter((event) => event.key === 'Escape'),
        takeUntilDestroyed(),
      )
      .subscribe(() => void this.requestClose());
    this.dialogRef
      .backdropClick()
      .pipe(takeUntilDestroyed())
      .subscribe(() => void this.requestClose());
    // An empty max price makes the band open-ended; max-inclusive no longer applies.
    this.form.controls.maxPrice.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((value) => this.openEnded.set(value == null || value === ''));
  }

  /** Server error for the max-inclusive toggle, if one is attached. */
  protected serverError(name: 'maxInclusive'): string | null {
    const message: unknown = this.form.controls[name].errors?.['server'];
    return typeof message === 'string' ? message : null;
  }

  /** The id of the toggle's inline error when one is shown, for aria-describedby; empty otherwise. */
  protected toggleErrorId(name: 'maxInclusive'): string {
    return this.serverError(name) ? `${name}-error` : '';
  }

  async requestClose(): Promise<void> {
    if (this.saving()) {
      return;
    }
    if (this.form.dirty) {
      const discard = await this.confirmService.confirm({
        message: this.transloco.translate('broker.editor.discardConfirm'),
        confirmLabel: this.transloco.translate('broker.editor.discard'),
        destructive: true,
      });
      if (!discard) {
        return;
      }
    }
    this.dialogRef.close(false);
  }

  /**
   * Effective-from is optional: an empty value is omitted, so the API uses now for a new band
   * and keeps the stored value for an edit. An untouched value is sent as the stored instant,
   * so seconds and sub-second precision survive a save.
   */
  private effectiveFromValue(): string | undefined {
    const local = this.form.controls.effectiveFrom.value;
    if (!local) {
      return undefined;
    }
    if (local === this.originalEffectiveLocal && this.originalEffectiveFrom) {
      return this.originalEffectiveFrom;
    }
    const date = new Date(local);
    return Number.isNaN(date.getTime()) ? undefined : date.toISOString();
  }

  async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      focusFirstInvalid(this.host.nativeElement);
      this.cdr.markForCheck();
      return;
    }
    this.saving.set(true);
    this.banner.set([]);
    this.relatedHint.set(false);
    const value = this.form.getRawValue();
    const maxPrice = value.maxPrice ?? null;
    const request: CommissionRateRequest = {
      maxPrice,
      maxInclusive: maxPrice === null ? false : value.maxInclusive,
      ratePercent: value.ratePercent ?? 0,
      effectiveFrom: this.effectiveFromValue(),
    };

    try {
      const id = this.data.rate?.id ?? null;
      if (id) {
        await this.dataService.update(id, request, this.editingVersion);
      } else {
        await this.dataService.create(request);
      }
      this.form.markAsPristine();
      this.dialogRef.close(true);
      this.snack.open(this.transloco.translate('common.saved'), undefined, { duration: 3000 });
    } catch (error) {
      if (error instanceof ApiError && error.status === 412) {
        this.saving.set(false);
        const reload = await this.confirmService.confirm({
          message: this.transloco.translate('admin.staleReload'),
          confirmLabel: this.transloco.translate('common.reload'),
        });
        if (reload) {
          this.dialogRef.close(true);
        }
        return;
      }
      this.banner.set(
        applyServerErrors(this.form, error, {
          inlineFields: INLINE_FIELDS,
          fallbackMessage: this.transloco.translate('errors.saveFailed'),
        }),
      );
      this.relatedHint.set(
        error instanceof ApiError && !error.fieldErrors && error.relatedIds.length > 0,
      );
      // Render the new error state first so focus lands on a control that is marked invalid.
      this.cdr.detectChanges();
      focusFirstInvalid(this.host.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }
}
