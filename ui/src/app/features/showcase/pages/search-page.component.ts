import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import type { OnInit } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { SearchStore } from '../data/search.store';
import { EmptyStateComponent } from '../../../shared/ui/empty-state.component';
import { ErrorStateComponent } from '../../../shared/ui/error-state.component';
import { LoadingStateComponent } from '../../../shared/ui/loading-state.component';
import { HbPricePipe } from '../../../shared/pipes/hb-price.pipe';
import type { ListingSearchCriteria } from '../../../core/data/listings-data.service';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

@Component({
  selector: 'hb-search-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [SearchStore],
  imports: [
    ReactiveFormsModule,
    RouterLink,
    TranslocoPipe,
    LoadingStateComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    HbPricePipe,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  template: `
    <section class="search-page">
      <h1>{{ 'search.title' | transloco }}</h1>
      <mat-card appearance="outlined" class="filters-card">
        <mat-card-content>
          <form [formGroup]="criteriaForm" (ngSubmit)="applyFilters()" class="filters">
            <mat-form-field appearance="outline">
              <mat-label>{{ 'search.location' | transloco }}</mat-label>
              <input
                matInput
                id="search-location"
                name="location"
                formControlName="location"
                [attr.placeholder]="'search.location' | transloco"
              />
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>{{ 'search.minPrice' | transloco }}</mat-label>
              <input
                matInput
                id="search-min-price"
                name="minPrice"
                type="number"
                formControlName="minPrice"
                [attr.placeholder]="'search.minPrice' | transloco"
              />
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>{{ 'search.maxPrice' | transloco }}</mat-label>
              <input
                matInput
                id="search-max-price"
                name="maxPrice"
                type="number"
                formControlName="maxPrice"
                [attr.placeholder]="'search.maxPrice' | transloco"
              />
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>{{ 'search.propertyType' | transloco }}</mat-label>
              <mat-select
                id="search-property-type"
                name="propertyTypeId"
                formControlName="propertyTypeId"
              >
                <mat-option value="">{{ 'search.propertyType' | transloco }}</mat-option>
                @for (type of store.propertyTypes(); track type.id) {
                  <mat-option [value]="type.id">{{ type.name }}</mat-option>
                }
              </mat-select>
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>{{ 'search.bedrooms' | transloco }}</mat-label>
              <input
                matInput
                id="search-bedrooms"
                name="bedrooms"
                type="number"
                formControlName="bedrooms"
                [attr.placeholder]="'search.bedrooms' | transloco"
              />
            </mat-form-field>

            <div class="actions">
              <button mat-raised-button color="primary" type="submit" [disabled]="store.loading()">
                {{ 'search.submit' | transloco }}
              </button>
            </div>
          </form>
        </mat-card-content>
      </mat-card>

      @if (store.loading()) {
        <hb-loading-state [label]="'search.loading' | transloco" />
      } @else if (store.error()) {
        <hb-error-state
          [title]="'search.errorTitle' | transloco"
          [description]="store.error() ?? undefined"
        />
      } @else if (store.results().length === 0) {
        <hb-empty-state
          [title]="'search.emptyTitle' | transloco"
          [description]="'search.emptyDescription' | transloco"
        />
      } @else {
        <div class="results">
          @for (listing of store.results(); track listing.id) {
            <mat-card appearance="outlined" class="result-card">
              <mat-card-header>
                <mat-card-title>
                  <a [routerLink]="['/listings', listing.id]">{{ listing.title }}</a>
                </mat-card-title>
                <mat-card-subtitle>{{ listing.city }}, {{ listing.locality }}</mat-card-subtitle>
              </mat-card-header>
              <mat-card-content>
                <p class="price">{{ listing.price | hbPrice }}</p>
              </mat-card-content>
            </mat-card>
          }
        </div>
      }
    </section>
  `,
  styles: `
    .search-page {
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
    }
    .filters-card {
      width: 100%;
    }
    .filters {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(14rem, 1fr));
      gap: 1rem;
      align-items: center;
    }
    .actions {
      display: flex;
      align-items: end;
      justify-content: flex-end;
    }
    .results {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
      gap: 1rem;
    }
    .result-card a {
      text-decoration: none;
      color: inherit;
    }
    .price {
      font-size: 1.1rem;
      font-weight: 600;
      margin: 0;
    }
  `,
})
export class SearchPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly store = inject(SearchStore);

  readonly criteriaForm = new FormGroup({
    location: new FormControl<string>('', { nonNullable: true }),
    minPrice: new FormControl<number | null>(null),
    maxPrice: new FormControl<number | null>(null),
    propertyTypeId: new FormControl<string>('', { nonNullable: true }),
    bedrooms: new FormControl<number | null>(null),
  });

  constructor() {
    this.route.queryParams.pipe(takeUntilDestroyed()).subscribe((params) => {
      this.criteriaForm.patchValue(
        {
          location: typeof params['location'] === 'string' ? params['location'] : '',
          minPrice: this.toNumber(params['minPrice']),
          maxPrice: this.toNumber(params['maxPrice']),
          propertyTypeId:
            typeof params['propertyTypeId'] === 'string' ? params['propertyTypeId'] : '',
          bedrooms: this.toNumber(params['bedrooms']),
        },
        { emitEvent: false },
      );
      void this.executeSearch(this.formToCriteria());
    });

    this.criteriaForm.controls.location.valueChanges
      .pipe(debounceTime(350), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe(() => {
        void this.applyFilters();
      });
  }

  ngOnInit(): void {
    void this.store.init();
  }

  async applyFilters(): Promise<void> {
    const criteria = this.formToCriteria();
    await this.router.navigate([], {
      relativeTo: this.route,
      queryParams: criteria,
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  private formToCriteria(): ListingSearchCriteria {
    const value = this.criteriaForm.getRawValue();
    return {
      location: value.location || undefined,
      minPrice: value.minPrice ?? undefined,
      maxPrice: value.maxPrice ?? undefined,
      propertyTypeId: value.propertyTypeId || undefined,
      bedrooms: value.bedrooms ?? undefined,
    };
  }

  private async executeSearch(criteria: ListingSearchCriteria): Promise<void> {
    await this.store.search(criteria);
  }

  private toNumber(value: unknown): number | null {
    if (typeof value === 'string' && value.length > 0) {
      const number = Number(value);
      return Number.isNaN(number) ? null : number;
    }
    if (typeof value === 'number') {
      return value;
    }
    return null;
  }
}
