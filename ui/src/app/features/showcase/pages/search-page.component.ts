import { Component, inject } from '@angular/core';
import type { OnInit } from '@angular/core';
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

@Component({
  selector: 'hb-search-page',
  standalone: true,
  providers: [SearchStore],
  imports: [
    ReactiveFormsModule,
    RouterLink,
    LoadingStateComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    HbPricePipe,
  ],
  template: `
    <section class="search-page">
      <h1>Search listings</h1>
      <form [formGroup]="criteriaForm" (ngSubmit)="applyFilters()" class="filters">
        <input formControlName="location" placeholder="Location" />
        <input type="number" formControlName="minPrice" placeholder="Min price" />
        <input type="number" formControlName="maxPrice" placeholder="Max price" />
        <select formControlName="propertyTypeId">
          <option value="">Any type</option>
          @for (type of store.propertyTypes(); track type.id) {
            <option [value]="type.id">{{ type.name }}</option>
          }
        </select>
        <input type="number" formControlName="bedrooms" placeholder="Bedrooms" />
        <button type="submit" [disabled]="store.loading()">Search</button>
      </form>

      @if (store.loading()) {
        <hb-loading-state label="Loading listings..." />
      } @else if (store.error()) {
        <hb-error-state
          title="Could not load listings"
          [description]="store.error() ?? undefined"
        />
      } @else if (store.results().length === 0) {
        <hb-empty-state title="No listings found" description="Try adjusting your filters." />
      } @else {
        <ul class="results">
          @for (listing of store.results(); track listing.id) {
            <li>
              <a [routerLink]="['/listings', listing.id]">{{ listing.title }}</a>
              <span>{{ listing.price | hbPrice }}</span>
              <span>{{ listing.city }}, {{ listing.locality }}</span>
            </li>
          }
        </ul>
      }
    </section>
  `,
  styles: `
    .search-page {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .filters {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
    }
    .results {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      padding: 0;
      list-style: none;
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
