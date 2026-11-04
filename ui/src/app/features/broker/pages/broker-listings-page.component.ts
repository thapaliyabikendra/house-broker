import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { HttpEventType } from '@angular/common/http';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ListingsDataService } from '../../../core/data/listings-data.service';
import { RuntimeConfigService } from '../../../core/config/runtime-config.service';
import { EmptyStateComponent } from '../../../shared/ui/empty-state.component';
import { ErrorStateComponent } from '../../../shared/ui/error-state.component';
import { LoadingStateComponent } from '../../../shared/ui/loading-state.component';
import { HbPricePipe } from '../../../shared/pipes/hb-price.pipe';
import type { OwnerListingDto } from '../../../api/models';
import { ApiError } from '../../../core/http/api-error';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

@Component({
  selector: 'hb-broker-listings-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    LoadingStateComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    HbPricePipe,
    TranslocoPipe,
    MatCardModule,
    MatButtonModule,
    MatChipsModule,
    MatProgressBarModule,
    MatSnackBarModule,
  ],
  template: `
    <section class="my-listings">
      <div class="header-row">
        <h1>{{ 'broker.listingsTitle' | transloco }}</h1>
        <a mat-raised-button color="primary" routerLink="/broker/listings/new">{{
          'broker.create' | transloco
        }}</a>
      </div>

      @if (loading()) {
        <hb-loading-state [label]="'broker.loading' | transloco" />
      } @else if (error()) {
        <hb-error-state
          [title]="error() ?? ('common.error' | transloco)"
          (retry)="load()"
          [actionLabel]="'common.retry' | transloco"
        />
      } @else if (listings().length === 0) {
        <hb-empty-state
          [title]="'broker.emptyTitle' | transloco"
          [description]="'broker.emptyDescription' | transloco"
        />
      } @else {
        <div class="listing-stack">
          @for (listing of listings(); track listing.id) {
            <mat-card appearance="outlined">
              <mat-card-header>
                <mat-card-title>{{ listing.title }}</mat-card-title>
                <mat-card-subtitle>
                  {{ listing.price | hbPrice }} ·
                  <mat-chip-set>
                    <mat-chip>{{ listing.status }}</mat-chip>
                  </mat-chip-set>
                </mat-card-subtitle>
              </mat-card-header>
              <mat-card-content>
                <p class="commission">
                  {{ 'broker.commission' | transloco }}:
                  <strong>{{ listing.commissionAmount ?? 0 | hbPrice }}</strong>
                </p>

                <div class="actions">
                  <a mat-stroked-button [routerLink]="['/broker/listings', listing.id, 'edit']">{{
                    'broker.edit' | transloco
                  }}</a>
                  @if ((listing.status ?? '').toLowerCase() === 'draft') {
                    <button
                      mat-flat-button
                      color="primary"
                      type="button"
                      (click)="publish(listing.id ?? '')"
                    >
                      {{ 'broker.publish' | transloco }}
                    </button>
                  }
                  @if ((listing.status ?? '').toLowerCase() === 'active') {
                    <button mat-stroked-button type="button" (click)="markSold(listing.id ?? '')">
                      {{ 'broker.sell' | transloco }}
                    </button>
                    <button mat-stroked-button type="button" (click)="archive(listing.id ?? '')">
                      {{ 'broker.archive' | transloco }}
                    </button>
                  }
                  @if ((listing.status ?? '').toLowerCase() === 'sold') {
                    <button mat-stroked-button type="button" (click)="archive(listing.id ?? '')">
                      {{ 'broker.archive' | transloco }}
                    </button>
                  }
                  <button mat-button color="warn" type="button" (click)="remove(listing.id ?? '')">
                    {{ 'broker.delete' | transloco }}
                  </button>
                </div>

                @if (listing.images?.length) {
                  <div class="images">
                    @for (image of listing.images; track image.id; let i = $index) {
                      <figure class="image-item">
                        <img
                          [src]="imageUrl(listing.id ?? '', image.id ?? '')"
                          [alt]="(listing.title || ('detail.imageAlt' | transloco)) + ' ' + (i + 1)"
                          width="128"
                          height="96"
                          loading="lazy"
                        />
                        @if (image.isPrimary) {
                          <mat-chip>{{ 'broker.primaryImage' | transloco }}</mat-chip>
                        }
                        <button
                          mat-button
                          color="warn"
                          type="button"
                          (click)="deleteImage(listing.id ?? '', image.id ?? '')"
                          [disabled]="deletingImageId() === image.id"
                        >
                          {{ 'broker.deleteImage' | transloco }}
                        </button>
                      </figure>
                    }
                  </div>
                }

                <div class="upload">
                  <label class="upload-label">
                    {{ 'broker.uploadImage' | transloco }}
                    <input
                      #fileInput
                      type="file"
                      accept="image/jpeg,image/png,image/webp"
                      (change)="upload(listing.id ?? '', $event)"
                      [disabled]="uploadingId() === listing.id"
                    />
                  </label>
                  @if (uploadingId() === listing.id && uploadProgress() !== null) {
                    <mat-progress-bar
                      mode="determinate"
                      [value]="uploadProgress()"
                    ></mat-progress-bar>
                    <span>{{ uploadProgress() }}%</span>
                  }
                  @if (uploadingId() === listing.id) {
                    <span>{{ 'broker.uploading' | transloco }}</span>
                  }
                </div>
              </mat-card-content>
            </mat-card>
          }
        </div>
      }
    </section>
  `,
  styles: `
    .my-listings {
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
    }
    .header-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 1rem;
    }
    .listing-stack {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .commission {
      margin: 0 0 0.75rem;
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      margin-block: 0.75rem;
    }
    .images {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
      margin-block: 0.75rem;
    }
    .image-item {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      align-items: center;
    }
    .image-item img {
      inline-size: 8rem;
      block-size: 6rem;
      object-fit: cover;
      border-radius: 6px;
    }
    .upload {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      max-inline-size: 20rem;
      margin-block-start: 0.75rem;
    }
    .upload-label {
      font-weight: 500;
    }
  `,
})
export class BrokerListingsPageComponent {
  private readonly listingsData = inject(ListingsDataService);
  private readonly config = inject(RuntimeConfigService);
  private readonly transloco = inject(TranslocoService);
  readonly router = inject(Router);
  private readonly snack = inject(MatSnackBar);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly listings = signal<OwnerListingDto[]>([]);
  readonly uploadingId = signal<string | null>(null);
  readonly uploadProgress = signal<number | null>(null);
  readonly deletingImageId = signal<string | null>(null);

  constructor() {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.listings.set(await this.listingsData.getMine());
    } catch (error) {
      this.error.set(
        error instanceof Error ? error.message : this.transloco.translate('errors.loadFailed'),
      );
    } finally {
      this.loading.set(false);
    }
  }

  async publish(id: string): Promise<void> {
    if (!window.confirm(this.transloco.translate('broker.publishConfirm'))) {
      return;
    }
    await this.transition(id, (listing) => this.listingsData.publish(id, listing.version));
  }

  async markSold(id: string): Promise<void> {
    if (!window.confirm(this.transloco.translate('broker.sellConfirm'))) {
      return;
    }
    await this.transition(id, (listing) => this.listingsData.markSold(id, listing.version));
  }

  async archive(id: string): Promise<void> {
    if (!window.confirm(this.transloco.translate('broker.archiveConfirm'))) {
      return;
    }
    await this.transition(id, (listing) => this.listingsData.archive(id, listing.version));
  }

  async remove(id: string): Promise<void> {
    if (!window.confirm(this.transloco.translate('broker.deleteConfirm'))) {
      return;
    }
    try {
      await this.listingsData.delete(id);
      this.listings.update((items) => items.filter((item) => item.id !== id));
      this.snack.open(this.transloco.translate('common.saved'), undefined, { duration: 3000 });
    } catch (error) {
      this.error.set(
        error instanceof Error ? error.message : this.transloco.translate('errors.deleteFailed'),
      );
    }
  }

  imageUrl(listingId: string, imageId: string): string {
    if (!listingId || !imageId) {
      return '';
    }
    return `${this.config.value().apiBaseUrl}/v1/listings/${listingId}/images/${imageId}`;
  }

  async deleteImage(listingId: string, imageId: string): Promise<void> {
    if (!window.confirm(this.transloco.translate('broker.deleteImageConfirm'))) {
      return;
    }
    this.deletingImageId.set(imageId);
    try {
      await this.listingsData.deleteImage(listingId, imageId);
      await this.load();
    } catch (error) {
      this.error.set(
        error instanceof Error ? error.message : this.transloco.translate('errors.deleteFailed'),
      );
    } finally {
      this.deletingImageId.set(null);
    }
  }

  private readonly allowedImageTypes = ['image/jpeg', 'image/png', 'image/webp'];
  private readonly maxImageSizeBytes = 5 * 1024 * 1024;

  upload(id: string, event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }
    if (!this.allowedImageTypes.includes(file.type)) {
      this.error.set(this.transloco.translate('broker.uploadTypeError'));
      input.value = '';
      return;
    }
    if (file.size > this.maxImageSizeBytes) {
      this.error.set(this.transloco.translate('broker.uploadSizeError'));
      input.value = '';
      return;
    }

    this.uploadingId.set(id);
    this.uploadProgress.set(0);
    this.listingsData.uploadImageProgress(id, file).subscribe({
      next: (event) => {
        if (event.type === HttpEventType.UploadProgress && event.total) {
          this.uploadProgress.set(Math.round((event.loaded / event.total) * 100));
        } else if (event.type === HttpEventType.Response) {
          this.uploadProgress.set(100);
        }
      },
      error: (error: unknown) => {
        if (error instanceof ApiError && error.fieldErrors) {
          for (const messages of Object.values(error.fieldErrors)) {
            const first = messages?.[0];
            if (first) {
              this.error.set(first.message || this.transloco.translate('errors.uploadFailed'));
            }
          }
        }
        this.error.set(
          error instanceof Error ? error.message : this.transloco.translate('errors.uploadFailed'),
        );
        this.uploadingId.set(null);
        this.uploadProgress.set(null);
        input.value = '';
      },
      complete: () => {
        this.uploadingId.set(null);
        this.uploadProgress.set(null);
        input.value = '';
        void this.load();
      },
    });
  }

  private async transition(
    id: string,
    action: (listing: OwnerListingDto) => Promise<OwnerListingDto>,
  ): Promise<void> {
    try {
      const listing = await this.listingsData.getMineById(id);
      await action(listing);
      this.snack.open(this.transloco.translate('common.saved'), undefined, { duration: 3000 });
      await this.load();
    } catch (error) {
      this.error.set(
        error instanceof Error ? error.message : this.transloco.translate('errors.statusFailed'),
      );
    }
  }
}
