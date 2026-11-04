import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '../../../core/auth/auth.service';
import { ListingsDataService } from '../../../core/data/listings-data.service';
import { RuntimeConfigService } from '../../../core/config/runtime-config.service';
import { EmptyStateComponent } from '../../../shared/ui/empty-state.component';
import { ErrorStateComponent } from '../../../shared/ui/error-state.component';
import { LoadingStateComponent } from '../../../shared/ui/loading-state.component';
import { HbPricePipe } from '../../../shared/pipes/hb-price.pipe';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import type { ListingDetailDto } from '../../../api/models';

@Component({
  selector: 'hb-listing-detail-page',
  standalone: true,
  imports: [
    LoadingStateComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    HbPricePipe,
    TranslocoPipe,
  ],
  template: `
    <section class="detail-page">
      @if (loading()) {
        <hb-loading-state [label]="'detail.loading' | transloco" />
      } @else if (error()) {
        <hb-error-state
          [title]="'detail.errorTitle' | transloco"
          [description]="error() ?? undefined"
        />
      } @else if (!listing()) {
        <hb-empty-state
          [title]="'detail.emptyTitle' | transloco"
          [description]="'detail.emptyDescription' | transloco"
        />
      } @else {
        @let item = listing()!;
        <h1>{{ item.title }}</h1>
        <p class="price">{{ item.price | hbPrice }}</p>
        <p>{{ item.city }}, {{ item.locality }}, {{ item.province }}</p>
        <p>
          {{ item.bedrooms }} {{ 'detail.bedrooms' | transloco }} · {{ item.bathrooms }}
          {{ 'detail.bathrooms' | transloco }} · {{ item.areaSqFt }} {{ 'detail.sqft' | transloco }}
        </p>
        <p>{{ item.description }}</p>

        @if (item.images && item.images.length > 0) {
          <div class="gallery">
            @for (image of item.images; track image.id) {
              <img
                [src]="imageUrl(item.id ?? '', image.id ?? '')"
                [alt]="item.title || ('detail.imageAlt' | transloco)"
              />
            }
          </div>
        }

        @if (item.features && item.features.length > 0) {
          <ul class="features">
            @for (feature of item.features; track feature.id) {
              <li>{{ feature.name || feature.code }}</li>
            }
          </ul>
        }

        <section class="contact">
          <h2>{{ 'detail.contactTitle' | transloco }}</h2>
          @if (canViewContact() && item.brokerContact) {
            <p>{{ item.brokerContact.fullName }}</p>
            @if (item.brokerContact.email) {
              <p>{{ item.brokerContact.email }}</p>
            }
            @if (item.brokerContact.phoneNumber) {
              <p>{{ item.brokerContact.phoneNumber }}</p>
            }
          } @else {
            <p>{{ 'detail.contactPrompt' | transloco }}</p>
            <button type="button" (click)="auth.signIn()">{{ 'detail.signIn' | transloco }}</button>
          }
        </section>
      }
    </section>
  `,
  styles: `
    .detail-page {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .gallery {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
    }
    .gallery img {
      inline-size: 12rem;
      block-size: 9rem;
      object-fit: cover;
    }
  `,
})
export class ListingDetailPageComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly listingsData = inject(ListingsDataService);
  private readonly config = inject(RuntimeConfigService);
  private readonly transloco = inject(TranslocoService);
  readonly auth = inject(AuthService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly listing = signal<ListingDetailDto | null>(null);

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const id = params.get('id');
      if (id) {
        void this.load(id);
      }
    });
  }

  canViewContact(): boolean {
    return this.auth.hasPermission('contacts.read') || this.auth.hasScope('contacts.read');
  }

  imageUrl(listingId: string, imageId: string): string {
    if (!listingId || !imageId) {
      return '';
    }
    return `${this.config.value().apiBaseUrl}/v1/listings/${listingId}/images/${imageId}`;
  }

  private async load(id: string): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.listing.set(await this.listingsData.getById(id));
    } catch (error) {
      this.error.set(
        error instanceof Error ? error.message : this.transloco.translate('errors.loadFailed'),
      );
      this.listing.set(null);
    } finally {
      this.loading.set(false);
    }
  }
}
