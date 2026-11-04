import { ChangeDetectionStrategy, Component, ElementRef, inject, signal } from '@angular/core';
import { ChangeDetectorRef } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import type { CommissionRateDto, CommissionRateRequest } from '../../../api/models';
import { AdminCommissionRatesDataService } from '../../../core/data/admin-commission-rates-data.service';
import { ApiError } from '../../../core/http/api-error';
import { ConfirmService } from '../../../shared/ui/confirm.service';
import { focusFirstInvalid } from '../../../shared/forms/focus-first-invalid';

export interface CommissionRateEditorDialogData {
  rate: CommissionRateDto | null;
}

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
    MatCheckboxModule,
    MatSlideToggleModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <h2 mat-dialog-title>
      {{ data.rate ? ('admin.edit' | transloco) : ('admin.newRate' | transloco) }}
    </h2>
    <mat-dialog-content class="dialog-body">
      @if (actionError()) {
        <p class="action-error" role="alert">{{ actionError() }}</p>
      }
      <form id="rate-form" [formGroup]="form" (ngSubmit)="submit()" class="rate-form">
        <mat-form-field appearance="outline">
          <mat-label>{{ 'admin.minPrice' | transloco }}</mat-label>
          <input matInput type="number" formControlName="minPrice" />
          @if (form.controls.minPrice.invalid && form.controls.minPrice.touched) {
            <mat-error>{{
              form.controls.minPrice.errors?.['server'] ?? ('admin.minPriceError' | transloco)
            }}</mat-error>
          }
        </mat-form-field>

        <mat-slide-toggle formControlName="minInclusive" color="primary">
          {{ 'admin.minInclusive' | transloco }}
        </mat-slide-toggle>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'admin.maxPrice' | transloco }}</mat-label>
          <input matInput type="number" formControlName="maxPrice" />
          @if (form.controls.maxPrice.invalid && form.controls.maxPrice.touched) {
            <mat-error>{{
              form.controls.maxPrice.errors?.['server'] ?? ('errors.validation' | transloco)
            }}</mat-error>
          }
        </mat-form-field>

        <mat-slide-toggle formControlName="maxInclusive" color="primary">
          {{ 'admin.maxInclusive' | transloco }}
        </mat-slide-toggle>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'admin.ratePercent' | transloco }}</mat-label>
          <input matInput type="number" formControlName="ratePercent" />
          <span matTextSuffix>%</span>
          @if (form.controls.ratePercent.invalid && form.controls.ratePercent.touched) {
            <mat-error>{{
              form.controls.ratePercent.errors?.['server'] ?? ('admin.ratePercentError' | transloco)
            }}</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'admin.effectiveFrom' | transloco }}</mat-label>
          <input matInput type="datetime-local" formControlName="effectiveFrom" />
          @if (form.controls.effectiveFrom.invalid && form.controls.effectiveFrom.touched) {
            <mat-error>{{
              form.controls.effectiveFrom.errors?.['server'] ??
                ('admin.effectiveFromError' | transloco)
            }}</mat-error>
          }
        </mat-form-field>

        <mat-slide-toggle formControlName="isActive" color="primary">
          {{ 'admin.active' | transloco }}
        </mat-slide-toggle>
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
    .rate-form .mat-mdc-slide-toggle {
      align-self: center;
      padding-block: 0.5rem;
    }
    .rate-form mat-form-field:has(input[type='datetime-local']) {
      grid-column: 1 / -1;
    }
    .action-error {
      color: var(--mat-sys-error, #b3261e);
      margin: 0 0 0.75rem;
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
  readonly actionError = signal<string | null>(null);
  readonly editingVersion = signal<string | null>(this.data.rate?.version ?? null);

  readonly form = new FormGroup({
    minPrice: new FormControl<number | null>(
      this.data.rate?.minPrice != null ? Number(this.data.rate.minPrice) : null,
      {
        validators: [Validators.required, Validators.min(0)],
      },
    ),
    minInclusive: new FormControl<boolean>(this.data.rate?.minInclusive ?? true, {
      nonNullable: true,
    }),
    maxPrice: new FormControl<number | null>(
      this.data.rate?.maxPrice != null ? Number(this.data.rate.maxPrice) : null,
    ),
    maxInclusive: new FormControl<boolean>(this.data.rate?.maxInclusive ?? false, {
      nonNullable: true,
    }),
    ratePercent: new FormControl<number | null>(
      this.data.rate?.ratePercent != null ? Number(this.data.rate.ratePercent) : null,
      {
        validators: [Validators.required, Validators.min(0)],
      },
    ),
    effectiveFrom: new FormControl<string>(
      this.data.rate?.effectiveFrom != null
        ? this.toLocalInputValue(String(this.data.rate.effectiveFrom))
        : '',
      { nonNullable: true, validators: [Validators.required] },
    ),
    isActive: new FormControl<boolean>(this.data.rate?.isActive ?? true, { nonNullable: true }),
  });

  constructor() {
    this.dialogRef
      .keydownEvents?.()
      .pipe(takeUntilDestroyed())
      .subscribe((event) => {
        if (event.key === 'Escape') {
          void this.requestClose();
        }
      });
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
      this.cdr.markForCheck();
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

    try {
      const id = this.data.rate?.id ?? null;
      if (id) {
        await this.dataService.update(id, request, this.editingVersion());
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
      this.cdr.markForCheck();
      this.actionError.set(
        this.firstFieldMessage(error) ??
          (error instanceof Error ? error.message : this.transloco.translate('errors.saveFailed')),
      );
    } finally {
      this.saving.set(false);
    }
  }
}
