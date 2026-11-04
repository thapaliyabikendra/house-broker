import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import type { AbstractControl, ValidationErrors } from '@angular/forms';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '../../../core/auth/auth.service';
import { ListingsDataService } from '../../../core/data/listings-data.service';
import { LookupsDataService } from '../../../core/data/lookups-data.service';
import { ApiError } from '../../../core/http/api-error';
import { ErrorStateComponent } from '../../../shared/ui/error-state.component';
import { LoadingStateComponent } from '../../../shared/ui/loading-state.component';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import type {
  PropertyTypeDto,
  CreateListingRequest,
  UpdateListingRequest,
} from '../../../api/models';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

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

@Component({
  selector: 'hb-broker-listing-editor',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    LoadingStateComponent,
    ErrorStateComponent,
    TranslocoPipe,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <section class="editor">
      <mat-card appearance="outlined">
        <mat-card-header>
          <mat-card-title>
            {{
              isEdit()
                ? ('broker.editor.editTitle' | transloco)
                : ('broker.editor.createTitle' | transloco)
            }}
          </mat-card-title>
        </mat-card-header>
        <mat-card-content>
          @if (loading()) {
            <hb-loading-state [label]="'broker.editor.loading' | transloco" />
          } @else {
            @if (error()) {
              <hb-error-state [title]="error() ?? ('common.error' | transloco)" />
            }

            <form [formGroup]="form" (ngSubmit)="submit()" class="editor-form">
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

              <mat-form-field appearance="outline">
                <mat-label>{{ 'broker.editor.price' | transloco }}</mat-label>
                <input matInput type="number" formControlName="price" />
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

              <div class="actions">
                <button
                  mat-raised-button
                  color="primary"
                  type="submit"
                  [disabled]="saving() || form.invalid"
                >
                  @if (saving()) {
                    <mat-spinner diameter="20" />
                  }
                  {{
                    saving()
                      ? ('broker.editor.saving' | transloco)
                      : ('broker.editor.save' | transloco)
                  }}
                </button>
              </div>
            </form>
          }
        </mat-card-content>
      </mat-card>
    </section>
  `,
  styles: `
    .editor {
      display: flex;
      justify-content: center;
      padding-block-start: 1rem;
    }
    mat-card {
      max-inline-size: 36rem;
      width: 100%;
    }
    .editor-form {
      display: grid;
      grid-template-columns: 1fr;
      gap: 1rem;
    }
    .actions {
      display: flex;
      justify-content: flex-end;
    }
  `,
})
export class BrokerListingEditorComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly listingsData = inject(ListingsDataService);
  private readonly lookupsData = inject(LookupsDataService);
  private readonly transloco = inject(TranslocoService);
  readonly auth = inject(AuthService);

  readonly isEdit = signal(false);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly propertyTypes = signal<PropertyTypeDto[]>([]);
  readonly version = signal<string | null>(null);
  readonly idempotencyKey = crypto.randomUUID();

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
    postalCode: new FormControl('', { validators: [Validators.pattern(/^\\d{5}$/)] }),
    bedrooms: new FormControl<number | null>(null, { validators: [numberRange(0, 20)] }),
    bathrooms: new FormControl<number | null>(null, { validators: [numberRange(0, 20)] }),
    areaSqFt: new FormControl<number | null>(null, { validators: [Validators.min(0)] }),
  });

  constructor() {
    this.lookupsData
      .propertyTypes()
      .then((types) => this.propertyTypes.set(types))
      .catch(() => this.propertyTypes.set([]));

    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const id = params.get('id');
      if (id) {
        this.isEdit.set(true);
        void this.load(id);
      } else {
        this.isEdit.set(false);
      }
    });
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
      });
    } catch (error) {
      this.error.set(
        error instanceof Error ? error.message : this.transloco.translate('errors.loadFailed'),
      );
    } finally {
      this.loading.set(false);
    }
  }

  async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    const value = this.form.getRawValue();
    const selectedFeatures: string[] = [];
    const payload = {
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
      featureIds: selectedFeatures,
    } as CreateListingRequest;

    try {
      const id = this.route.snapshot.paramMap.get('id');
      if (this.isEdit() && id) {
        await this.listingsData.update(
          id,
          payload as UpdateListingRequest,
          this.version() ?? undefined,
        );
      } else {
        await this.listingsData.create(payload as CreateListingRequest, this.idempotencyKey);
      }
      await this.router.navigate(['/broker/listings']);
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
