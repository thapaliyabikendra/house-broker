import { Component, inject, signal } from '@angular/core';
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
import { TranslocoPipe } from '@jsverse/transloco';
import type {
  PropertyTypeDto,
  CreateListingRequest,
  UpdateListingRequest,
} from '../../../api/models';

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
  imports: [ReactiveFormsModule, LoadingStateComponent, ErrorStateComponent, TranslocoPipe],
  template: `
    <section class="editor">
      <h1>{{ isEdit() ? 'Edit listing' : 'Create listing' }}</h1>

      @if (loading()) {
        <hb-loading-state [label]="'broker.editor.loading' | transloco" />
      } @else {
        @if (error()) {
          <hb-error-state [title]="error() ?? 'Error'" />
        }

        <form [formGroup]="form" (ngSubmit)="submit()" class="editor-form">
          <label>
            {{ 'broker.editor.title' | transloco }}
            <input formControlName="title" />
            @if (form.controls.title.invalid && form.controls.title.touched) {
              <small>Title is required or too long.</small>
            }
          </label>

          <label>
            {{ 'broker.editor.description' | transloco }}
            <textarea formControlName="description"></textarea>
          </label>

          <label>
            {{ 'broker.editor.price' | transloco }}
            <input type="number" formControlName="price" />
            @if (form.controls.price.invalid && form.controls.price.touched) {
              <small>Price is required and must be valid.</small>
            }
          </label>

          <label>
            {{ 'broker.editor.propertyType' | transloco }}
            <select formControlName="propertyTypeId">
              @for (type of propertyTypes(); track type.id) {
                <option [value]="type.id">{{ type.name }}</option>
              }
            </select>
          </label>

          <label>
            {{ 'broker.editor.city' | transloco }}
            <input formControlName="city" />
            @if (form.controls.city.invalid && form.controls.city.touched) {
              <small>City is required.</small>
            }
          </label>

          <label>
            {{ 'broker.editor.locality' | transloco }}
            <input formControlName="locality" />
          </label>

          <label>
            {{ 'broker.editor.province' | transloco }}
            <input formControlName="province" />
          </label>

          <label>
            {{ 'broker.editor.postalCode' | transloco }}
            <input formControlName="postalCode" />
            @if (form.controls.postalCode.invalid && form.controls.postalCode.touched) {
              <small>Postal code must be 5 digits.</small>
            }
          </label>

          <label>
            {{ 'broker.editor.bedrooms' | transloco }}
            <input type="number" formControlName="bedrooms" />
          </label>

          <label>
            {{ 'broker.editor.bathrooms' | transloco }}
            <input type="number" formControlName="bathrooms" />
          </label>

          <label>
            {{ 'broker.editor.area' | transloco }}
            <input type="number" formControlName="areaSqFt" />
          </label>

          <button type="submit" [disabled]="saving() || form.invalid">
            {{
              saving() ? ('broker.editor.saving' | transloco) : ('broker.editor.save' | transloco)
            }}
          </button>
        </form>
      }
    </section>
  `,
  styles: `
    .editor {
      display: flex;
      flex-direction: column;
      gap: 1rem;
      max-inline-size: 36rem;
    }
    .editor-form {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }
    label {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }
  `,
})
export class BrokerListingEditorComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly listingsData = inject(ListingsDataService);
  private readonly lookupsData = inject(LookupsDataService);
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
      // Feature selection is intentionally not tracked by this simplified form.
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : 'Failed to load listing');
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
      await this.router.navigate(['/search']);
    } catch (error) {
      if (error instanceof ApiError && error.fieldErrors) {
        for (const [field, messages] of Object.entries(error.fieldErrors)) {
          const control = this.form.get(field);
          if (control && messages?.length) {
            control.setErrors({ server: messages[0] });
          }
        }
      }
      this.error.set(error instanceof Error ? error.message : 'Save failed');
    } finally {
      this.saving.set(false);
    }
  }
}
