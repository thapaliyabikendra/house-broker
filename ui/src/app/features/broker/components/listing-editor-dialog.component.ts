import { ChangeDetectionStrategy, Component, ElementRef, inject, signal } from '@angular/core';
import type { AbstractControl, ValidationErrors } from '@angular/forms';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { filter } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { ListingsDataService } from '../../../core/data/listings-data.service';
import { LookupsDataService } from '../../../core/data/lookups-data.service';
import { ApiError } from '../../../core/http/api-error';
import type { FeatureDto, CreateListingRequest, PropertyTypeDto } from '../../../api/models';
import { focusFirstInvalid } from '../../../shared/forms/focus-first-invalid';
import { ConfirmService } from '../../../shared/ui/confirm.service';
import { ErrorStateComponent } from '../../../shared/ui/error-state.component';
import { LoadingStateComponent } from '../../../shared/ui/loading-state.component';

/** Input for the dialog: the listing to edit, or null to create one. */
export interface ListingEditorDialogData {
  listingId: string | null;
}

function numberRange(min: number, max: number) {
  return (control: AbstractControl): ValidationErrors | null => {
    const value: unknown = control.value;
    if (value === null || value === undefined || value === '') {
      return null;
    }
    const number = Number(value);
    if (Number.isNaN(number) || number < min || number > max) {
      return { range: { min, max } };
    }
    return null;
  };
}

/**
 * Create or edit a listing in a modal. Closes with `true` after a successful save and
 * `false` when the user cancels. Unsaved edits are confirmed before the dialog closes.
 */
@Component({
  selector: 'hb-listing-editor-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    LoadingStateComponent,
    ErrorStateComponent,
    TranslocoPipe,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <h2 mat-dialog-title>
      {{
        isEdit()
          ? ('broker.editor.editTitle' | transloco)
          : ('broker.editor.createTitle' | transloco)
      }}
    </h2>

    <mat-dialog-content class="dialog-body">
      @if (loading()) {
        <hb-loading-state [label]="'broker.editor.loading' | transloco" />
      } @else {
        @if (error()) {
          <hb-error-state [title]="error() ?? ('common.error' | transloco)" />
        }

        <form id="listing-form" [formGroup]="form" (ngSubmit)="submit()" class="editor-form">
          <h3 class="group-title">{{ 'broker.editor.groupBasics' | transloco }}</h3>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'broker.editor.title' | transloco }}</mat-label>
            <input matInput formControlName="title" />
            @if (form.controls.title.invalid && form.controls.title.touched) {
              <mat-error>{{ 'broker.editor.titleError' | transloco }}</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>{{ 'broker.editor.description' | transloco }}</mat-label>
            <textarea matInput formControlName="description" rows="4"></textarea>
          </mat-form-field>

          <h3 class="group-title">{{ 'broker.editor.groupPriceType' | transloco }}</h3>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'broker.editor.price' | transloco }}</mat-label>
            <input matInput type="number" formControlName="price" />
            <mat-hint>{{ 'broker.editor.priceHint' | transloco }}</mat-hint>
            @if (form.controls.price.invalid && form.controls.price.touched) {
              <mat-error>{{ 'broker.editor.priceError' | transloco }}</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>{{ 'broker.editor.propertyType' | transloco }}</mat-label>
            <mat-select formControlName="propertyTypeId">
              @for (type of propertyTypes(); track type.id) {
                <mat-option [value]="type.id">{{ type.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <h3 class="group-title">{{ 'broker.editor.groupLocation' | transloco }}</h3>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'broker.editor.city' | transloco }}</mat-label>
            <input matInput formControlName="city" />
            @if (form.controls.city.invalid && form.controls.city.touched) {
              <mat-error>{{ 'broker.editor.cityError' | transloco }}</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>{{ 'broker.editor.locality' | transloco }}</mat-label>
            <input matInput formControlName="locality" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>{{ 'broker.editor.province' | transloco }}</mat-label>
            <input matInput formControlName="province" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>{{ 'broker.editor.postalCode' | transloco }}</mat-label>
            <input matInput formControlName="postalCode" />
            @if (form.controls.postalCode.invalid && form.controls.postalCode.touched) {
              <mat-error>{{ 'broker.editor.postalCodeError' | transloco }}</mat-error>
            }
          </mat-form-field>

          <h3 class="group-title">{{ 'broker.editor.groupDetails' | transloco }}</h3>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'broker.editor.bedrooms' | transloco }}</mat-label>
            <input matInput type="number" formControlName="bedrooms" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>{{ 'broker.editor.bathrooms' | transloco }}</mat-label>
            <input matInput type="number" formControlName="bathrooms" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>{{ 'broker.editor.area' | transloco }}</mat-label>
            <input matInput type="number" formControlName="areaSqFt" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>{{ 'broker.editor.features' | transloco }}</mat-label>
            <mat-select formControlName="featureIds" multiple>
              @for (feature of features(); track feature.id) {
                <mat-option [value]="feature.id">{{ feature.name || feature.code }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        </form>
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="requestClose()">
        {{ 'common.cancel' | transloco }}
      </button>
      <button
        mat-raised-button
        color="primary"
        type="submit"
        form="listing-form"
        [disabled]="saving() || loading()"
      >
        @if (saving()) {
          <mat-spinner diameter="20" />
        }
        {{ saving() ? ('broker.editor.saving' | transloco) : ('broker.editor.save' | transloco) }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .dialog-body {
      max-block-size: none;
    }
    .editor-form {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 0.5rem 1rem;
      padding-block-start: 0.5rem;
    }
    .group-title {
      grid-column: 1 / -1;
      margin: 1rem 0 0.25rem;
      font-size: 1.05rem;
      font-weight: 500;
    }
    .editor-form mat-form-field:has(textarea) {
      grid-column: 1 / -1;
    }
    @media (max-width: 720px) {
      .editor-form {
        grid-template-columns: minmax(0, 1fr);
      }
    }
  `,
})
export class ListingEditorDialogComponent {
  private readonly dialogRef =
    inject<MatDialogRef<ListingEditorDialogComponent, boolean>>(MatDialogRef);
  private readonly listingId: string | null =
    inject<ListingEditorDialogData>(MAT_DIALOG_DATA).listingId;
  private readonly listingsData = inject(ListingsDataService);
  private readonly lookupsData = inject(LookupsDataService);
  private readonly transloco = inject(TranslocoService);
  readonly auth = inject(AuthService);
  private readonly snack = inject(MatSnackBar);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly confirmService = inject(ConfirmService);

  readonly isEdit = signal(this.listingId !== null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly propertyTypes = signal<PropertyTypeDto[]>([]);
  readonly features = signal<FeatureDto[]>([]);
  readonly version = signal<string | null>(null);
  /** Key and serialized body of the last create attempt; a changed body gets a fresh key. */
  private idempotency: { key: string; body: string } | null = null;

  readonly form = new FormGroup({
    title: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    description: new FormControl('', { nonNullable: true }),
    price: new FormControl<number | null>(null, {
      validators: [Validators.required, Validators.min(0)],
    }),
    propertyTypeId: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    city: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    locality: new FormControl(''),
    province: new FormControl(''),
    postalCode: new FormControl('', { validators: [Validators.pattern(/^\d{5}$/)] }),
    bedrooms: new FormControl<number | null>(null, { validators: [numberRange(0, 20)] }),
    bathrooms: new FormControl<number | null>(null, { validators: [numberRange(0, 20)] }),
    areaSqFt: new FormControl<number | null>(null, { validators: [Validators.min(0)] }),
    featureIds: new FormControl<string[]>([], { nonNullable: true }),
  });

  constructor() {
    this.lookupsData
      .propertyTypes()
      .then((types) => this.propertyTypes.set(types))
      .catch(() => this.propertyTypes.set([]));
    this.lookupsData
      .features()
      .then((items) => this.features.set(items))
      .catch(() => this.features.set([]));

    // The dialog is opened with disableClose so every exit goes through requestClose(),
    // which asks before discarding edits.
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

    if (this.listingId) {
      void this.load(this.listingId);
    }
  }

  /** Closes without saving, after confirming when the form has unsaved edits. */
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

  private async load(id: string): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const listing = await this.listingsData.getMineById(id);
      this.version.set(listing.version ?? null);
      this.form.patchValue({
        title: listing.title ?? '',
        description: listing.description ?? '',
        price: typeof listing.price === 'string' ? Number(listing.price) : (listing.price ?? null),
        propertyTypeId: listing.propertyTypeId ?? '',
        city: listing.city ?? '',
        locality: listing.locality ?? '',
        province: listing.province ?? '',
        postalCode: listing.postalCode ?? '',
        bedrooms: listing.bedrooms != null ? Number(listing.bedrooms) : null,
        bathrooms: listing.bathrooms != null ? Number(listing.bathrooms) : null,
        areaSqFt: listing.areaSqFt != null ? Number(listing.areaSqFt) : null,
        featureIds: listing.features?.map((f) => f.id ?? '').filter(Boolean) ?? [],
      });
    } catch (error) {
      this.error.set(
        error instanceof Error ? error.message : this.transloco.translate('errors.loadFailed'),
      );
    } finally {
      this.loading.set(false);
    }
  }

  /**
   * 412 means someone else saved first. Offer a reload; the user's unsaved edits are
   * replaced only if they agree.
   */
  private async offerReload(): Promise<void> {
    const reload = await this.confirmService.confirm({
      message: this.transloco.translate('broker.editor.staleReload'),
      confirmLabel: this.transloco.translate('common.reload'),
    });
    if (reload && this.listingId) {
      await this.load(this.listingId);
    }
  }

  private idempotencyKeyFor(payload: CreateListingRequest): string {
    const body = JSON.stringify(payload);
    if (this.idempotency?.body !== body) {
      this.idempotency = { key: crypto.randomUUID(), body };
    }
    return this.idempotency.key;
  }

  async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      focusFirstInvalid(this.host.nativeElement);
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    const value = this.form.getRawValue();
    const payload: CreateListingRequest = {
      title: value.title,
      description: value.description,
      price: value.price ?? 0,
      propertyTypeId: value.propertyTypeId,
      city: value.city,
      locality: value.locality || undefined,
      province: value.province || undefined,
      postalCode: value.postalCode || undefined,
      bedrooms: value.bedrooms ?? undefined,
      bathrooms: value.bathrooms ?? undefined,
      areaSqFt: value.areaSqFt ?? undefined,
      featureIds: value.featureIds ?? [],
    };

    try {
      if (this.listingId) {
        await this.listingsData.update(this.listingId, payload, this.version() ?? undefined);
      } else {
        await this.listingsData.create(payload, this.idempotencyKeyFor(payload));
      }
      this.snack.open(this.transloco.translate('common.saved'), undefined, { duration: 3000 });
      this.form.markAsPristine();
      this.dialogRef.close(true);
    } catch (error) {
      if (error instanceof ApiError && error.status === 412) {
        this.saving.set(false);
        await this.offerReload();
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
      this.error.set(
        error instanceof Error ? error.message : this.transloco.translate('errors.saveFailed'),
      );
    } finally {
      this.saving.set(false);
    }
  }
}
