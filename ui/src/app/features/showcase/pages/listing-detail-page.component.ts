import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
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
    RouterLink,
    MatCardModule,
    MatButtonModule,
  ],
  template: `
    <section class="detail-page">
      <a mat-button routerLink="/search" class="back-link">{{ 'detail.back' | transloco }}</a>

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
        <div class="detail-layout">
          <div class="main-column">
            <mat-card appearance="outlined" class="overview">
              <mat-card-header>
                <mat-card-title class="title">{{ item.title }}</mat-card-title>
                <mat-card-subtitle
                  >{{ item.city }}, {{ item.locality }}, {{ item.province }}</mat-card-subtitle
                >
              </mat-card-header>
              <mat-card-content>
                <p class="price">{{ item.price | hbPrice }}</p>

                <dl class="facts">
                  <div>
                    <dt>{{ 'detail.factBedrooms' | transloco }}</dt>
                    <dd>{{ item.bedrooms ?? '–' }}</dd>
                  </div>
                  <div>
                    <dt>{{ 'detail.factBathrooms' | transloco }}</dt>
                    <dd>{{ item.bathrooms ?? '–' }}</dd>
                  </div>
                  <div>
                    <dt>{{ 'detail.factArea' | transloco }}</dt>
                    <dd>{{ item.areaSqFt ?? '–' }} {{ 'detail.sqft' | transloco }}</dd>
                  </div>
                </dl>

                @if (item.description) {
                  <h2 class="section-title">{{ 'detail.description' | transloco }}</h2>
                  <p class="description">{{ item.description }}</p>
                }

                @if (item.features && item.features.length > 0) {
                  <h2 class="section-title">{{ 'detail.features' | transloco }}</h2>
                  <ul class="features">
                    @for (feature of item.features; track feature.id) {
                      <li>{{ feature.name || feature.code }}</li>
                    }
                  </ul>
                }
              </mat-card-content>
            </mat-card>

            @if (item.images && item.images.length > 0) {
              <div class="gallery">
                @for (image of item.images; track image.id; let i = $index) {
                  <img
                    class="gallery-image"
                    [class.hero-image]="i === 0"
                    [src]="imageUrl(item.id ?? '', image.id ?? '')"
                    [alt]="(item.title || ('detail.imageAlt' | transloco)) + ' ' + (i + 1)"
                    loading="lazy"
                  />
                }
              </div>
            }
          </div>

          <aside class="side-column">
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
          </aside>
        </div>
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
      gap: 1rem;
    }
    .back-link {
      align-self: flex-start;
    }
    .detail-layout {
      display: grid;
      grid-template-columns: minmax(0, 2fr) minmax(16rem, 1fr);
      gap: 1.5rem;
      align-items: start;
    }
    .main-column {
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
      min-inline-size: 0;
    }
    .side-column {
      position: sticky;
      inset-block-start: 1rem;
    }
    .title {
      font-size: 1.5rem;
    }
    .price {
      font-size: 1.5rem;
      font-weight: 600;
      margin: 0.5rem 0 1rem;
    }
    .facts {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      gap: 1rem;
      margin: 0 0 1rem;
    }
    .facts dt {
      color: var(--mat-sys-on-surface-variant);
      font-size: 0.85rem;
    }
    .facts dd {
      margin: 0;
      font-weight: 500;
    }
    .section-title {
      font-size: 1.05rem;
      margin: 1.25rem 0 0.5rem;
    }
    .description {
      white-space: pre-line;
      margin: 0;
    }
    .features {
      margin: 0;
      padding-inline-start: 1.25rem;
      columns: 2;
    }
    .gallery {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(10rem, 1fr));
      gap: 0.75rem;
    }
    .gallery-image {
      inline-size: 100%;
      block-size: auto;
      aspect-ratio: 4 / 3;
      object-fit: cover;
      border-radius: 8px;
    }
    .gallery-image.hero-image {
      grid-column: 1 / -1;
      aspect-ratio: 16 / 9;
    }
    .contact-name {
      font-weight: 600;
    }
    @media (max-width: 900px) {
      .detail-layout {
        grid-template-columns: minmax(0, 1fr);
      }
      .side-column {
        position: static;
      }
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
