import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import type { OnInit } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
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
import { MatPaginatorModule } from '@angular/material/paginator';
import type { PageEvent } from '@angular/material/paginator';

type FilterKey = 'location' | 'minPrice' | 'maxPrice' | 'propertyTypeId' | 'bedrooms' | 'sort';

interface ActiveFilter {
  key: FilterKey;
  labelKey: string;
  kind: 'text' | 'price' | 'number';
  value?: string | number;
  /** Translation key for a value that is a fixed option (for example a sort order). */
  valueKey?: string;
}

const SORT_LABEL_KEYS: Record<string, string> = {
  priceAsc: 'search.sortPriceAsc',
  priceDesc: 'search.sortPriceDesc',
};

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
    MatPaginatorModule,
  ],
  template: `
    <section class="search-page">
      <h1>{{ 'search.title' | transloco }}</h1>

      <mat-card appearance="outlined" class="filters-card">
        <mat-card-content>
          <form [formGroup]="criteriaForm" (ngSubmit)="applyFilters(true)" class="filters">
            <mat-form-field appearance="outline">
              <mat-label>{{ 'search.location' | transloco }}</mat-label>
              <input matInput id="search-location" name="location" formControlName="location" />
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>{{ 'search.minPrice' | transloco }}</mat-label>
              <input
                matInput
                id="search-min-price"
                name="minPrice"
                type="number"
                min="0"
                formControlName="minPrice"
              />
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>{{ 'search.maxPrice' | transloco }}</mat-label>
              <input
                matInput
                id="search-max-price"
                name="maxPrice"
                type="number"
                min="0"
                formControlName="maxPrice"
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
                min="0"
                formControlName="bedrooms"
              />
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>{{ 'search.sort' | transloco }}</mat-label>
              <mat-select formControlName="sort">
                <mat-option value="">{{ 'search.sortDefault' | transloco }}</mat-option>
                <mat-option value="priceAsc">{{ 'search.sortPriceAsc' | transloco }}</mat-option>
                <mat-option value="priceDesc">{{ 'search.sortPriceDesc' | transloco }}</mat-option>
              </mat-select>
            </mat-form-field>

            <div class="actions">
              <button mat-button type="button" (click)="clearFilters()">
                {{ 'search.clearFilters' | transloco }}
              </button>
              <button mat-raised-button color="primary" type="submit" [disabled]="store.loading()">
                {{ 'search.submit' | transloco }}
              </button>
            </div>
          </form>
        </mat-card-content>
      </mat-card>

      @if (activeFilters().length > 0) {
        <div class="active-filters" role="group" [attr.aria-label]="'search.activeFilters' | transloco">
          @for (filter of activeFilters(); track filter.key) {
            <button type="button" class="filter-chip" (click)="removeFilter(filter.key)">
              <span>{{ filter.labelKey | transloco }}:</span>
              @if (filter.kind === 'price') {
                <span>{{ filter.value | hbPrice }}</span>
              } @else if (filter.valueKey) {
                <span>{{ filter.valueKey | transloco }}</span>
              } @else {
                <span>{{ filter.value }}</span>
              }
              <span class="visually-hidden">{{ 'search.removeFilter' | transloco }}</span>
              <span aria-hidden="true">×</span>
            </button>
          }
        </div>
      }

      @if (store.loading()) {
        <hb-loading-state [label]="'search.loading' | transloco" />
      } @else if (store.error()) {
        <hb-error-state
          [title]="'search.errorTitle' | transloco"
          [description]="store.error() ?? undefined"
          [actionLabel]="'common.retry' | transloco"
          (retry)="store.search(store.criteria())"
        />
      } @else if (store.results().length === 0) {
        <hb-empty-state
          [title]="'search.emptyTitle' | transloco"
          [description]="'search.emptyDescription' | transloco"
        />
      } @else {
        <p class="result-count" aria-live="polite">
          {{ 'search.resultCount' | transloco: { count: store.total() ?? 0 } }}
        </p>
        <div class="results">
          @for (listing of store.results(); track listing.id) {
            <mat-card appearance="outlined" class="result-card">
              <mat-card-header>
                <mat-card-title>
                  <a class="stretched" [routerLink]="['/listings', listing.id]">{{
                    listing.title
                  }}</a>
                </mat-card-title>
                <mat-card-subtitle>{{ listing.city }}, {{ listing.locality }}</mat-card-subtitle>
              </mat-card-header>
              <mat-card-content>
                <p class="price">{{ listing.price | hbPrice }}</p>
                <p class="facts">
                  {{ listing.bedrooms ?? '–' }} {{ 'detail.bedrooms' | transloco }} ·
                  {{ listing.bathrooms ?? '–' }} {{ 'detail.bathrooms' | transloco }}
                </p>
              </mat-card-content>
            </mat-card>
          }
        </div>
        <mat-paginator
          [length]="store.total() ?? 0"
          [pageSize]="store.pageSize() ?? 10"
          [pageIndex]="(store.page() ?? 1) - 1"
          (page)="onPage($event)"
          showFirstLastButtons
        />
      }
    </section>
  `,
  styles: `
    .search-page {
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
    }
    .filters {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      gap: 0 1rem;
      align-items: center;
    }
    .actions {
      grid-column: 1 / -1;
      display: flex;
      align-items: center;
      justify-content: flex-end;
      gap: 0.5rem;
    }
    .active-filters {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      margin-block-start: -0.5rem;
    }
    .filter-chip {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      padding: 0.25rem 0.75rem;
      border: 1px solid var(--mat-sys-outline);
      border-radius: 999px;
      background: var(--mat-sys-surface-container-low);
      color: var(--mat-sys-on-surface);
      font: inherit;
      cursor: pointer;
    }
    .filter-chip:focus-visible {
      outline: 2px solid var(--mat-sys-primary);
      outline-offset: 2px;
    }
    .result-count {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
    }
    .results {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
      gap: 1rem;
    }
    .result-card {
      position: relative;
    }
    /* Stretches the title link over the whole card so the entire card is clickable. */
    .stretched::after {
      content: '';
      position: absolute;
      inset: 0;
    }
    .stretched {
      color: inherit;
      text-decoration: none;
    }
    .stretched:focus-visible {
      outline: none;
    }
    .stretched:focus-visible::after {
      outline: 2px solid var(--mat-sys-primary);
      outline-offset: 2px;
    }
    .price {
      font-size: 1.1rem;
      font-weight: 600;
      margin: 0 0 0.25rem;
    }
    .facts {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
    }
    .visually-hidden {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip: rect(0 0 0 0);
      white-space: nowrap;
    }
    @media (max-width: 900px) {
      .filters {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }
    }
  `,
})
export class SearchPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly store = inject(SearchStore);

  readonly pageIndex = signal(0);

  readonly criteriaForm = new FormGroup({
    location: new FormControl<string>('', { nonNullable: true }),
    minPrice: new FormControl<number | null>(null),
    maxPrice: new FormControl<number | null>(null),
    propertyTypeId: new FormControl<string>('', { nonNullable: true }),
    bedrooms: new FormControl<number | null>(null),
    sort: new FormControl<string>('', { nonNullable: true }),
  });

  /** Filters currently applied (from the URL), shown as removable chips. */
  readonly activeFilters = computed<ActiveFilter[]>(() => {
    const criteria = this.store.criteria();
    const filters: ActiveFilter[] = [];
    if (criteria.location) {
      filters.push({
        key: 'location',
        labelKey: 'search.location',
        kind: 'text',
        value: criteria.location,
      });
    }
    if (criteria.minPrice != null) {
      filters.push({
        key: 'minPrice',
        labelKey: 'search.minPrice',
        kind: 'price',
        value: criteria.minPrice,
      });
    }
    if (criteria.maxPrice != null) {
      filters.push({
        key: 'maxPrice',
        labelKey: 'search.maxPrice',
        kind: 'price',
        value: criteria.maxPrice,
      });
    }
    if (criteria.propertyTypeId) {
      const name = this.store
        .propertyTypes()
        .find((type) => type.id === criteria.propertyTypeId)?.name;
      filters.push({
        key: 'propertyTypeId',
        labelKey: 'search.propertyType',
        kind: 'text',
        value: name ?? criteria.propertyTypeId,
      });
    }
    if (criteria.bedrooms != null) {
      filters.push({
        key: 'bedrooms',
        labelKey: 'search.bedrooms',
        kind: 'number',
        value: criteria.bedrooms,
      });
    }
    if (criteria.sort) {
      filters.push({
        key: 'sort',
        labelKey: 'search.sort',
        kind: 'text',
        valueKey: SORT_LABEL_KEYS[criteria.sort],
      });
    }
    return filters;
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
          sort: typeof params['sort'] === 'string' ? params['sort'] : '',
        },
        { emitEvent: false },
      );
      const pageParam = this.toNumber(params['page']);
      if (pageParam && pageParam > 0) {
        this.pageIndex.set(pageParam - 1);
      } else {
        this.pageIndex.set(0);
      }
      void this.store.search(this.formToCriteria());
    });
  }

  ngOnInit(): void {
    void this.store.init();
  }

  /** Filters apply on submit only, so every field behaves the same way. */
  async applyFilters(resetPage = false): Promise<void> {
    if (resetPage) {
      this.pageIndex.set(0);
    }
    const criteria = this.formToCriteria();
    await this.router.navigate([], {
      relativeTo: this.route,
      queryParams: criteria,
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  /** Removes one filter from the URL; `undefined` drops the query parameter. */
  async removeFilter(key: FilterKey): Promise<void> {
    await this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { [key]: undefined, page: undefined },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  async clearFilters(): Promise<void> {
    this.criteriaForm.reset({ location: '', minPrice: null, maxPrice: null, propertyTypeId: '', bedrooms: null, sort: '' });
    await this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        location: undefined,
        minPrice: undefined,
        maxPrice: undefined,
        propertyTypeId: undefined,
        bedrooms: undefined,
        sort: undefined,
        page: undefined,
      },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    void this.applyFilters(false);
  }

  private formToCriteria(): ListingSearchCriteria {
    const value = this.criteriaForm.getRawValue();
    return {
      location: value.location || undefined,
      minPrice: value.minPrice ?? undefined,
      maxPrice: value.maxPrice ?? undefined,
      propertyTypeId: value.propertyTypeId || undefined,
      bedrooms: value.bedrooms ?? undefined,
      sort: value.sort || undefined,
      page: this.pageIndex() + 1,
      pageSize: 10,
    };
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
