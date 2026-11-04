import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ListingsDataService } from '../../../core/data/listings-data.service';
import { EmptyStateComponent } from '../../../shared/ui/empty-state.component';
import { ErrorStateComponent } from '../../../shared/ui/error-state.component';
import { LoadingStateComponent } from '../../../shared/ui/loading-state.component';
import { HbPricePipe } from '../../../shared/pipes/hb-price.pipe';
import { TranslocoPipe } from '@jsverse/transloco';
import type { OwnerListingDto } from '../../../api/models';

@Component({
  selector: 'hb-broker-listings-page',
  standalone: true,
  imports: [
    RouterLink,
    LoadingStateComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    HbPricePipe,
    TranslocoPipe,
  ],
  template: `
    <section class="my-listings">
      <h1>{{ 'broker.listingsTitle' | transloco }}</h1>
      <p>
        <a routerLink="/broker/listings/new">{{ 'broker.create' | transloco }}</a>
      </p>

      @if (loading()) {
        <hb-loading-state [label]="'broker.loading' | transloco" />
      } @else if (error()) {
        <hb-error-state [title]="error() ?? 'Error'" (retry)="load()" [actionLabel]="'Retry'" />
      } @else if (listings().length === 0) {
        <hb-empty-state
          [title]="'broker.emptyTitle' | transloco"
          [description]="'broker.emptyDescription' | transloco"
        />
      } @else {
        <ul class="listing-list">
          @for (listing of listings(); track listing.id) {
            <li>
              <strong>{{ listing.title }}</strong>
              <span>{{ listing.price | hbPrice }}</span>
              <span class="status-chip">{{ listing.status }}</span>
              <span
                >{{ 'broker.commission' | transloco }}:
                {{ listing.commissionAmount ?? 0 | hbPrice }}</span
              >

              <div class="actions">
                <a [routerLink]="['/broker/listings', listing.id, 'edit']">Edit</a>
                <button type="button" (click)="publish(listing.id ?? '')">Publish</button>
                <button type="button" (click)="markSold(listing.id ?? '')">Sell</button>
                <button type="button" (click)="archive(listing.id ?? '')">Archive</button>
                <button type="button" (click)="remove(listing.id ?? '')">Delete</button>
              </div>

              <div class="upload">
                <input
                  type="file"
                  (change)="upload(listing.id ?? '', $event)"
                  [disabled]="uploadingId() === listing.id"
                />
              </div>
            </li>
          }
        </ul>
      }
    </section>
  `,
  styles: `
    .my-listings {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .listing-list {
      display: flex;
      flex-direction: column;
      gap: 1rem;
      padding: 0;
      list-style: none;
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
    }
  `,
})
export class BrokerListingsPageComponent {
  private readonly listingsData = inject(ListingsDataService);
  readonly router = inject(Router);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly listings = signal<OwnerListingDto[]>([]);
  readonly uploadingId = signal<string | null>(null);

  constructor() {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const summaries = await this.listingsData.getMine();
      const details = await Promise.all(
        summaries
          .filter((item) => !!item.id)
          .map((item) => this.listingsData.getMineById(item.id as string)),
      );
      this.listings.set(details);
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : 'Failed to load listings');
    } finally {
      this.loading.set(false);
    }
  }

  async publish(id: string): Promise<void> {
    await this.transition(id, (listing) => this.listingsData.publish(id, listing.version));
  }

  async markSold(id: string): Promise<void> {
    await this.transition(id, (listing) => this.listingsData.markSold(id, listing.version));
  }

  async archive(id: string): Promise<void> {
    await this.transition(id, (listing) => this.listingsData.archive(id, listing.version));
  }

  async remove(id: string): Promise<void> {
    try {
      await this.listingsData.delete(id);
      this.listings.update((items) => items.filter((item) => item.id !== id));
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : 'Delete failed');
    }
  }

  private readonly allowedImageTypes = ['image/jpeg', 'image/png', 'image/webp'];
  private readonly maxImageSizeBytes = 5 * 1024 * 1024;

  async upload(id: string, event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }
    if (!this.allowedImageTypes.includes(file.type)) {
      this.error.set('Only JPEG, PNG, and WebP images are allowed.');
      input.value = '';
      return;
    }
    if (file.size > this.maxImageSizeBytes) {
      this.error.set('Image size must be 5 MB or smaller.');
      input.value = '';
      return;
    }
    this.uploadingId.set(id);
    try {
      await this.listingsData.uploadImage(id, file);
      await this.load();
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : 'Image upload failed');
    } finally {
      this.uploadingId.set(null);
      input.value = '';
    }
  }

  private async transition(
    id: string,
    action: (listing: OwnerListingDto) => Promise<OwnerListingDto>,
  ): Promise<void> {
    try {
      const listing = await this.listingsData.getMineById(id);
      await action(listing);
      await this.load();
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : 'Status change failed');
    }
  }
}
