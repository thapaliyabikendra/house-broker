import { TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { provideTransloco } from '@jsverse/transloco';
import type { TranslocoLoader } from '@jsverse/transloco';
import { of, Subject } from 'rxjs';
import { vi } from 'vitest';
import { ListingEditorDialogComponent } from './listing-editor-dialog.component';
import { ListingsDataService } from '../../../core/data/listings-data.service';
import { LookupsDataService } from '../../../core/data/lookups-data.service';
import { ConfirmService } from '../../../shared/ui/confirm.service';

@Injectable()
class TestLoader implements TranslocoLoader {
  getTranslation() {
    return of({});
  }
}

describe('ListingEditorDialogComponent', () => {
  function setup(confirmResult: boolean) {
    const close = vi.fn();
    const dialogRef = {
      close,
      keydownEvents: () => new Subject<KeyboardEvent>(),
      backdropClick: () => new Subject<MouseEvent>(),
    };
    const confirm = vi.fn().mockResolvedValue(confirmResult);
    const listingsData = { getMineById: vi.fn(), create: vi.fn(), update: vi.fn() };
    const lookupsData = {
      propertyTypes: vi.fn().mockResolvedValue([{ id: 't1', name: 'House' }]),
      features: vi.fn().mockResolvedValue([]),
    };

    TestBed.configureTestingModule({
      imports: [ListingEditorDialogComponent],
      providers: [
        { provide: MatDialogRef, useValue: dialogRef },
        { provide: MAT_DIALOG_DATA, useValue: { listingId: null } },
        { provide: ListingsDataService, useValue: listingsData },
        { provide: LookupsDataService, useValue: lookupsData },
        { provide: ConfirmService, useValue: { confirm } },
        provideTransloco({
          config: { availableLangs: ['en', 'ne'], defaultLang: 'en' },
          loader: TestLoader,
        }),
      ],
    });

    const component = TestBed.createComponent(ListingEditorDialogComponent).componentInstance;
    return { component, close, confirm, listingsData, lookupsData };
  }

  it('starts in create mode without a listing id and loads the lookups', async () => {
    const { component, listingsData, lookupsData } = setup(true);
    await Promise.resolve();

    expect(component.isEdit()).toBe(false);
    expect(lookupsData.propertyTypes).toHaveBeenCalled();
    expect(listingsData.getMineById).not.toHaveBeenCalled();
  });

  it('closes without asking when the form has no edits', async () => {
    const { component, close, confirm } = setup(true);

    await component.requestClose();

    expect(confirm).not.toHaveBeenCalled();
    expect(close).toHaveBeenCalledWith(false);
  });

  it('keeps the dialog open when the user chooses to keep editing', async () => {
    const { component, close, confirm } = setup(false);
    component.form.controls.title.setValue('Draft title');
    component.form.markAsDirty();

    await component.requestClose();

    expect(confirm).toHaveBeenCalledWith(expect.objectContaining({ destructive: true }));
    expect(close).not.toHaveBeenCalled();
  });

  it('discards edits and closes when the user confirms', async () => {
    const { component, close, confirm } = setup(true);
    component.form.controls.title.setValue('Draft title');
    component.form.markAsDirty();

    await component.requestClose();

    expect(confirm).toHaveBeenCalled();
    expect(close).toHaveBeenCalledWith(false);
  });
});
