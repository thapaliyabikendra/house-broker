import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { of } from 'rxjs';
import { Injectable } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import type { TranslocoLoader } from '@jsverse/transloco';
import { provideTransloco } from '@jsverse/transloco';
import {
  provideTranslocoLocale,
  provideTranslocoLocaleLangMapping,
} from '@jsverse/transloco-locale';
import { CommissionRatesAdminPageComponent } from './commission-rates-admin-page.component';
import { AdminCommissionRatesDataService } from '../../../core/data/admin-commission-rates-data.service';
import { CommissionRateEditorDialogComponent } from '../components/commission-rate-editor-dialog.component';

@Injectable()
class TestLoader implements TranslocoLoader {
  getTranslation() {
    return of({});
  }
}

function setup(saved: boolean) {
  const rate = {
    id: '1',
    minPrice: 0,
    maxPrice: 100,
    ratePercent: 2,
    isActive: true,
    version: 'v1',
  };
  const data = {
    list: vi.fn().mockResolvedValue([rate]),
    create: vi.fn(),
    update: vi.fn(),
  };
  const dialog = {
    open: vi.fn(() => ({ afterClosed: () => of(saved) })),
  };

  TestBed.configureTestingModule({
    imports: [CommissionRatesAdminPageComponent],
    providers: [
      { provide: AdminCommissionRatesDataService, useValue: data },
      { provide: MatDialog, useValue: dialog },
      provideTransloco({
        config: { availableLangs: ['en', 'ne'], defaultLang: 'en' },
        loader: TestLoader,
      }),
      provideTranslocoLocale(),
      provideTranslocoLocaleLangMapping({ en: 'en-IN', ne: 'ne-NP' }),
    ],
  });

  const fixture = TestBed.createComponent(CommissionRatesAdminPageComponent);
  return { fixture, data, dialog };
}

describe('CommissionRatesAdminPageComponent', () => {
  it('loads commission rates on init', async () => {
    const { fixture, data } = setup(false);
    await fixture.whenStable();

    expect(data.list).toHaveBeenCalled();
    expect(fixture.componentInstance.rates()).toHaveLength(1);
  });

  it('opens the create dialog at the shared width without closing on Escape or backdrop', async () => {
    const { fixture, dialog } = setup(false);
    await fixture.whenStable();

    fixture.componentInstance.startNew();

    expect(dialog.open).toHaveBeenCalledWith(
      CommissionRateEditorDialogComponent,
      expect.objectContaining({
        data: { rate: null },
        width: '40rem',
        disableClose: true,
      }),
    );
  });

  it('opens the edit dialog for the selected rate with the same width', async () => {
    const { fixture, dialog } = setup(false);
    await fixture.whenStable();
    const [first] = fixture.componentInstance.rates();
    if (!first) {
      throw new Error('expected a rate');
    }

    fixture.componentInstance.edit(first);

    expect(dialog.open).toHaveBeenCalledWith(
      CommissionRateEditorDialogComponent,
      expect.objectContaining({
        data: { rate: first },
        width: '40rem',
        disableClose: true,
      }),
    );
  });

  it('reloads the list after the dialog reports a save', async () => {
    const { fixture, data } = setup(true);
    await fixture.whenStable();
    expect(data.list).toHaveBeenCalledTimes(1);

    fixture.componentInstance.startNew();
    await fixture.whenStable();

    expect(data.list).toHaveBeenCalledTimes(2);
  });
});
