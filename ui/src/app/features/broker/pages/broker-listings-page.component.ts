import { ImageFallbackDirective } from '../../../shared/ui/image-fallback.directive';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import type { OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import {
  ListingEditorDialogComponent,
  type ListingEditorDialogData,
} from '../components/listing-editor-dialog.component';
import { HttpEventType } from '@angular/common/http';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AuthService } from '../../../core/auth/auth.service';
import { ListingsDataService } from '../../../core/data/listings-data.service';
import { RuntimeConfigService } from '../../../core/config/runtime-config.service';
import { EmptyStateComponent } from '../../../shared/ui/empty-state.component';
import { ErrorStateComponent } from '../../../shared/ui/error-state.component';
import { LoadingStateComponent } from '../../../shared/ui/loading-state.component';
import { HbPricePipe } from '../../../shared/pipes/hb-price.pipe';
import { ConfirmService } from '../../../shared/ui/confirm.service';
import type { OwnerListingDto } from '../../../api/models';
import { ApiError } from '../../../core/http/api-error';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

@Component({
  selector: 'hb-broker-listings-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ImageFallbackDirective,
    LoadingStateComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    HbPricePipe,
    TranslocoPipe,
    MatCardModule,
    MatButtonModule,
    MatChipsModule,
    MatMenuModule,
    MatProgressBarModule,
    MatSnackBarModule,
  ],
  template: `
    <section class="my-listings">
      <div class="header-row">
        <h1>{{ 'broker.listingsTitle' | transloco }}</h1>
        @if (canCreateListings()) {
          <button mat-raised-button color="primary" type="button" (click)="openEditor(null)">
            {{ 'broker.create' | transloco }}
          </button>
        }
      </div>

      @if (actionError()) {
        <p class="action-error" role="alert">{{ actionError() }}</p>
      }

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
        <div class="filter-bar">
          <mat-chip-listbox
            [attr.aria-label]="'broker.filter.label' | transloco"
            [value]="statusFilter()"
            (change)="setFilter($event.value)"
          >
            <mat-chip-option value="all"
              >{{ 'broker.filter.all' | transloco }} ({{ counts().all }})</mat-chip-option
            >
            <mat-chip-option value="draft"
              >{{ statusLabel('draft') }} ({{ counts().draft }})</mat-chip-option
            >
            <mat-chip-option value="active"
              >{{ statusLabel('active') }} ({{ counts().active }})</mat-chip-option
            >
            <mat-chip-option value="sold"
              >{{ statusLabel('sold') }} ({{ counts().sold }})</mat-chip-option
            >
            <mat-chip-option value="archived"
              >{{ statusLabel('archived') }} ({{ counts().archived }})</mat-chip-option
            >
          </mat-chip-listbox>
        </div>

        @if (visibleListings().length === 0) {
          <p class="filter-empty">{{ 'broker.filter.empty' | transloco }}</p>
        }

        <div class="listing-stack">
          @for (listing of visibleListings(); track listing.id) {
            <mat-card appearance="outlined">
              @if (coverImageId(listing); as imageId) {
                <!-- Decorative: the title beside it names the listing. -->
                <img
                  mat-card-image
                  class="cover"
                  alt=""
                  loading="lazy"
                  [src]="imageUrl(listing.id ?? '', imageId)"
                  hbImageFallback
                />
              } @else {
                <div class="cover cover-placeholder" aria-hidden="true"></div>
              }
              <mat-card-header>
                <mat-card-title>{{ listing.title }}</mat-card-title>
                <mat-card-subtitle>
                  {{ listing.price | hbPrice: wholeAmount }}
                  <mat-chip-set>
                    <mat-chip>{{ statusLabel(listing.status) }}</mat-chip>
                  </mat-chip-set>
                </mat-card-subtitle>
              </mat-card-header>
              <mat-card-content>
                <p class="location">
                  {{ listing.city }}, {{ listing.locality }} · {{ listing.bedrooms ?? '–' }}
                  {{ 'detail.bedrooms' | transloco }} · {{ listing.bathrooms ?? '–' }}
                  {{ 'detail.bathrooms' | transloco }}
                </p>
                <p class="commission">
                  {{ 'broker.commission' | transloco }}:
                  <strong>{{ listing.commissionAmount ?? 0 | hbPrice: wholeAmount }}</strong>
                </p>

                <div class="actions">
                  <button mat-stroked-button type="button" (click)="openEditor(listing.id ?? '')">
                    {{ 'broker.edit' | transloco }}
                  </button>
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
                  }
                  <button
                    mat-stroked-button
                    type="button"
                    [matMenuTriggerFor]="moreMenu"
                    [attr.aria-label]="
                      'broker.moreActionsFor' | transloco: { title: listing.title ?? '' }
                    "
                  >
                    {{ 'broker.more' | transloco }}
                  </button>
                  <mat-menu #moreMenu="matMenu">
                    @if ((listing.status ?? '').toLowerCase() !== 'archived') {
                      <button mat-menu-item type="button" (click)="archive(listing.id ?? '')">
                        {{ 'broker.archive' | transloco }}
                      </button>
                    }
                    <button
                      mat-menu-item
                      type="button"
                      class="danger-item"
                      (click)="remove(listing.id ?? '')"
                    >
                      {{ 'broker.delete' | transloco }}
                    </button>
                  </mat-menu>
                </div>

                <details class="photos">
                  <summary>
                    {{ 'broker.photos' | transloco }} ({{ listing.images?.length ?? 0 }})
                  </summary>
                  @if (listing.images?.length) {
                    <div class="images">
                      @for (image of listing.images; track image.id; let i = $index) {
                        <figure class="image-item">
                          <img
                            [src]="imageUrl(listing.id ?? '', image.id ?? '')"
                            hbImageFallback
                            [alt]="
                              'detail.photoAlt'
                                | transloco
                                  : {
                                      title: listing.title ?? '',
                                      index: i + 1,
                                      total: listing.images.length,
                                    }
                            "
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
                </details>
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
    .filter-bar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
    }
    .filter-empty {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
    }
    .danger-item {
      color: var(--mat-sys-error);
    }
    .cover {
      display: block;
      inline-size: 100%;
      block-size: 180px;
      object-fit: cover;
    }
    .cover-placeholder {
      background: var(--mat-sys-surface-container-high);
    }
    .location {
      margin: 0 0 0.5rem;
      color: var(--mat-sys-on-surface-variant);
    }
    .photos {
      margin-block-start: 0.75rem;
    }
    .photos summary {
      cursor: pointer;
      font-weight: 500;
      padding-block: 0.25rem;
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
    .action-error {
      color: var(--mat-sys-error, #b3261e);
      margin: 0;
    }
  `,
})
export class BrokerListingsPageComponent implements OnInit {
  private readonly auth = inject(AuthService, { optional: true });
  private readonly listingsData = inject(ListingsDataService);
  private readonly config = inject(RuntimeConfigService);
  private readonly transloco = inject(TranslocoService);
  readonly router = inject(Router);
  private readonly snack = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);
  private readonly route = inject(ActivatedRoute);
  private readonly confirmService = inject(ConfirmService);

  readonly loading = signal(false);
  /** Failure of the last load; replaces the list. */
  readonly error = signal<string | null>(null);
  /** Failure of the last action (publish, delete, upload...); shown inline, list stays visible. */
  readonly actionError = signal<string | null>(null);
  readonly listings = signal<OwnerListingDto[]>([]);
  readonly uploadingId = signal<string | null>(null);
  readonly uploadProgress = signal<number | null>(null);
  readonly deletingImageId = signal<string | null>(null);
  /** Status chip filter: 'all' or a lowercase status value. */
  readonly statusFilter = signal<string>('all');
  readonly visibleListings = computed(() => {
    const filter = this.statusFilter();
    if (filter === 'all') {
      return this.listings();
    }
    return this.listings().filter((item) => (item.status ?? '').toLowerCase() === filter);
  });

  setFilter(value: string): void {
    this.statusFilter.set(value);
  }

  /** Brokers may create listings; admins and seekers do not see the create action. */
  canCreateListings(): boolean {
    return this.auth?.hasPermission('listings:create') ?? true;
  }

  constructor() {
    void this.load();
  }

  /**
   * Deep links /broker/listings/new and /broker/listings/:id/edit open the same dialog on
   * this page; closing it returns to the plain list URL.
   */
  ngOnInit(): void {
    const mode = this.route.snapshot.data['editor'] as 'new' | 'edit' | undefined;
    if (mode === 'new') {
      this.openEditor(null, true);
    } else if (mode === 'edit') {
      this.openEditor(this.route.snapshot.paramMap.get('id'), true);
    }
  }

  /** Opens the create (listingId null) or edit dialog; reloads the list after a save. */
  openEditor(listingId: string | null, fromRoute = false): void {
    const ref = this.dialog.open<ListingEditorDialogComponent, ListingEditorDialogData, boolean>(
      ListingEditorDialogComponent,
      {
        data: { listingId },
        width: '56rem',
        maxWidth: '95vw',
        maxHeight: '90vh',
        disableClose: true,
      },
    );
    ref.afterClosed().subscribe((saved) => {
      if (saved) {
        void this.load();
      }
      if (fromRoute) {
        void this.router.navigate(['/broker/listings'], { replaceUrl: true });
      }
    });
  }

  readonly wholeAmount: Intl.NumberFormatOptions = { maximumFractionDigits: 0 };

  /** Chip counts per status, so the filter bar shows how many listings each view holds. */
  readonly counts = computed(() => {
    const items = this.listings();
    const countOf = (status: string) =>
      items.filter((item) => (item.status ?? '').toLowerCase() === status).length;
    return {
      all: items.length,
      draft: countOf('draft'),
      active: countOf('active'),
      sold: countOf('sold'),
      archived: countOf('archived'),
    };
  });

  /** Primary image if the listing has one, otherwise the first image; null when it has none. */
  coverImageId(listing: OwnerListingDto): string | null {
    const images = listing.images ?? [];
    return (images.find((image) => image.isPrimary) ?? images[0])?.id ?? null;
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
    const confirmed = await this.confirmService.confirm({
      message: this.transloco.translate('broker.publishConfirm'),
      confirmLabel: this.transloco.translate('broker.publish'),
    });
    if (!confirmed) {
      return;
    }
    await this.transition(
      id,
      (listing) => this.listingsData.publish(id, listing.version),
      this.transloco.translate('broker.published'),
    );
  }

  async markSold(id: string): Promise<void> {
    const confirmed = await this.confirmService.confirm({
      message: this.transloco.translate('broker.sellConfirm'),
      confirmLabel: this.transloco.translate('broker.sell'),
    });
    if (!confirmed) {
      return;
    }
    await this.transition(
      id,
      (listing) => this.listingsData.markSold(id, listing.version),
      this.transloco.translate('broker.sold'),
    );
  }

  async archive(id: string): Promise<void> {
    const confirmed = await this.confirmService.confirm({
      message: this.transloco.translate('broker.archiveConfirm'),
      confirmLabel: this.transloco.translate('broker.archive'),
    });
    if (!confirmed) {
      return;
    }
    await this.transition(
      id,
      (listing) => this.listingsData.archive(id, listing.version),
      this.transloco.translate('broker.archived'),
    );
  }

  async remove(id: string): Promise<void> {
    const confirmed = await this.confirmService.confirm({
      message: this.transloco.translate('broker.deleteConfirm'),
      confirmLabel: this.transloco.translate('broker.delete'),
      destructive: true,
    });
    if (!confirmed) {
      return;
    }
    this.actionError.set(null);
    try {
      await this.listingsData.delete(id);
      this.listings.update((items) => items.filter((item) => item.id !== id));
      this.snack.open(this.transloco.translate('common.deleted'), undefined, { duration: 3000 });
    } catch (error) {
      this.actionError.set(
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
    const confirmed = await this.confirmService.confirm({
      message: this.transloco.translate('broker.deleteImageConfirm'),
      confirmLabel: this.transloco.translate('broker.deleteImage'),
      destructive: true,
    });
    if (!confirmed) {
      return;
    }
    this.deletingImageId.set(imageId);
    this.actionError.set(null);
    try {
      await this.listingsData.deleteImage(listingId, imageId);
      await this.load();
    } catch (error) {
      this.actionError.set(
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
    this.actionError.set(null);
    if (!this.allowedImageTypes.includes(file.type)) {
      this.actionError.set(this.transloco.translate('broker.uploadTypeError'));
      input.value = '';
      return;
    }
    if (file.size > this.maxImageSizeBytes) {
      this.actionError.set(this.transloco.translate('broker.uploadSizeError'));
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
        this.actionError.set(
          this.firstFieldMessage(error) ??
            (error instanceof Error
              ? error.message
              : this.transloco.translate('errors.uploadFailed')),
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

  /** Localised label for a listing status; unknown values are shown as received. */
  statusLabel(status: string | undefined): string {
    // Literal keys (not a template string) so the Transloco key scanner can see them.
    switch ((status ?? '').toLowerCase()) {
      case 'draft':
        return this.transloco.translate('broker.status.draft');
      case 'active':
        return this.transloco.translate('broker.status.active');
      case 'sold':
        return this.transloco.translate('broker.status.sold');
      case 'archived':
        return this.transloco.translate('broker.status.archived');
      default:
        return status ?? '';
    }
  }

  private async transition(
    id: string,
    action: (listing: OwnerListingDto) => Promise<OwnerListingDto>,
    successMessage: string,
  ): Promise<void> {
    this.actionError.set(null);
    try {
      const listing = await this.listingsData.getMineById(id);
      await action(listing);
      this.snack.open(successMessage, undefined, { duration: 3000 });
      await this.load();
    } catch (error) {
      this.actionError.set(
        error instanceof Error ? error.message : this.transloco.translate('errors.statusFailed'),
      );
    }
  }

  /** First server-provided field message, if the failure carries field errors. */
  private firstFieldMessage(error: unknown): string | null {
    if (!(error instanceof ApiError) || !error.fieldErrors) {
      return null;
    }
    for (const messages of Object.values(error.fieldErrors)) {
      const first = messages?.[0];
      if (first?.message) {
        return first.message;
      }
    }
    return null;
  }
}
