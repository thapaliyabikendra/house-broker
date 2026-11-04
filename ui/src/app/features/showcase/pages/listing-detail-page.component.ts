import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
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
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';

@Component({
  selector: 'hb-listing-detail-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    LoadingStateComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    HbPricePipe,
    TranslocoPipe,
    MatCardModule,
    MatButtonModule,
    MatChipsModule,
  ],
  template: `
    <section class="detail-page">
      @if (loading()) {
        <hb-loading-state [label]="'detail.loading' | transloco" />
      } @else if (error()) {
        <hb-error-state
          [title]="'detail.errorTitle' | transloco"
          [description]="error() ?? undefined"
          [actionLabel]="'common.retry' | transloco"
          (retry)="retry()"
        />
      } @else if (listing(); as item) {
        <mat-card appearance="outlined" class="overview">
          <mat-card-header>
            <mat-card-title>{{ item.title }}</mat-card-title>
            <mat-card-subtitle
              >{{ item.city }}, {{ item.locality }}, {{ item.province }}</mat-card-subtitle
            >
          </mat-card-header>
          <mat-card-content>
            <p class="price">{{ item.price | hbPrice }}</p>
            <mat-chip-set>
              <mat-chip>{{ item.bedrooms }} {{ 'detail.bedrooms' | transloco }}</mat-chip>
              <mat-chip>{{ item.bathrooms }} {{ 'detail.bathrooms' | transloco }}</mat-chip>
              <mat-chip>{{ item.areaSqFt }} {{ 'detail.sqft' | transloco }}</mat-chip>
            </mat-chip-set>
            @if (item.description) {
              <p class="description">{{ item.description }}</p>
            }

            @if (item.images && item.images.length > 0) {
              <div class="gallery">
                @for (image of item.images; track image.id; let i = $index) {
                  <img
                    [src]="imageUrl(item.id ?? '', image.id ?? '')"
                    [alt]="(item.title || ('detail.imageAlt' | transloco)) + ' ' + (i + 1)"
                    width="192"
                    height="144"
                    loading="lazy"
                  />
                }
              </div>
            }

            @if (item.features && item.features.length > 0) {
              <mat-chip-set class="features">
                @for (feature of item.features; track feature.id) {
                  <mat-chip>{{ feature.name || feature.code }}</mat-chip>
                }
              </mat-chip-set>
            }
          </mat-card-content>
        </mat-card>

        <mat-card appearance="outlined" class="contact">
          <mat-card-header>
            <mat-card-title>{{ 'detail.contactTitle' | transloco }}</mat-card-title>
          </mat-card-header>
          <mat-card-content>
            @if (canViewContact() && item.brokerContact) {
              <p class="contact-name">{{ item.brokerContact.fullName }}</p>
              @if (item.brokerContact.email) {
                <p>{{ item.brokerContact.email }}</p>
              }
              @if (item.brokerContact.phoneNumber) {
                <p>{{ item.brokerContact.phoneNumber }}</p>
              }
            } @else {
              @if (auth.isAuthenticated()) {
                <p>{{ 'detail.contactDenied' | transloco }}</p>
              } @else {
                <p>{{ 'detail.contactPrompt' | transloco }}</p>
                <button mat-raised-button color="primary" type="button" (click)="auth.signIn()">
                  {{ 'detail.signIn' | transloco }}
                </button>
              }
            }
          </mat-card-content>
        </mat-card>
      } @else {
        <hb-empty-state
          [title]="'detail.emptyTitle' | transloco"
          [description]="'detail.emptyDescription' | transloco"
        />
      }
    </section>
  `,
  styles: `
    .detail-page {
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
    }
    .price {
      font-size: 1.5rem;
      font-weight: 600;
      margin: 0 0 0.75rem;
    }
    .gallery {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(9rem, 1fr));
      gap: 0.5rem;
      margin-block: 1rem;
    }
    .gallery img {
      inline-size: 100%;
      block-size: auto;
      aspect-ratio: 4 / 3;
      object-fit: cover;
      border-radius: 8px;
    }
    .description {
      margin-block-start: 1rem;
    }
    .contact-name {
      font-weight: 600;
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
  readonly lastId = signal<string | null>(null);
  private loadSeq = 0;

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const id = params.get('id');
      if (id) {
        this.lastId.set(id);
        void this.load(id);
      }
    });
  }

  retry(): void {
    const id = this.lastId();
    if (id) {
      void this.load(id);
    }
  }

  canViewContact(): boolean {
    return this.auth.hasPermission('listings:contact:view') && this.auth.hasScope('contacts.read');
  }

  imageUrl(listingId: string, imageId: string): string {
    if (!listingId || !imageId) {
      return '';
    }
    return `${this.config.value().apiBaseUrl}/v1/listings/${listingId}/images/${imageId}`;
  }

  private async load(id: string): Promise<void> {
    // Only the most recent load may update state; responses for earlier ids are discarded.
    const seq = ++this.loadSeq;
    this.loading.set(true);
    this.error.set(null);
    try {
      const listing = await this.listingsData.getById(id);
      if (seq === this.loadSeq) {
        this.listing.set(listing);
      }
    } catch (error) {
      if (seq !== this.loadSeq) {
        return;
      }
      this.error.set(
        error instanceof Error ? error.message : this.transloco.translate('errors.loadFailed'),
      );
      this.listing.set(null);
    } finally {
      if (seq === this.loadSeq) {
        this.loading.set(false);
      }
    }
  }
}
