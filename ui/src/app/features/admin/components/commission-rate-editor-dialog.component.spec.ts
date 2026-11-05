import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { Injectable } from '@angular/core';
import type { TranslocoLoader } from '@jsverse/transloco';
import { provideTransloco } from '@jsverse/transloco';
import { of, Subject } from 'rxjs';
import { vi } from 'vitest';
import type { Mock } from 'vitest';
import type { CommissionRateDto, CommissionRateRequest } from '../../../api/models';
import { AdminCommissionRatesDataService } from '../../../core/data/admin-commission-rates-data.service';
import { ApiError } from '../../../core/http/api-error';
import { ConfirmService } from '../../../shared/ui/confirm.service';
import { CommissionRateEditorDialogComponent } from './commission-rate-editor-dialog.component';

@Injectable()
class TestLoader implements TranslocoLoader {
  getTranslation() {
    return of({});
  }
}

type CreateFn = (request: CommissionRateRequest) => Promise<CommissionRateDto>;
type UpdateFn = (
  id: string,
  request: CommissionRateRequest,
  version?: string | null,
) => Promise<CommissionRateDto>;

interface SetupOptions {
  rate?: CommissionRateDto | null;
  create?: Mock<CreateFn>;
  update?: Mock<UpdateFn>;
}

const SAVED: CommissionRateDto = {} as CommissionRateDto;

function setup(options: SetupOptions = {}) {
  const close = vi.fn();
  const backdrop = new Subject<MouseEvent>();
  const dialogRef = {
    close,
    keydownEvents: () => new Subject<KeyboardEvent>(),
    backdropClick: () => backdrop,
  };
  const create = options.create ?? vi.fn<CreateFn>().mockResolvedValue(SAVED);
  const update = options.update ?? vi.fn<UpdateFn>().mockResolvedValue(SAVED);
  const confirm = vi.fn().mockResolvedValue(false);

  TestBed.configureTestingModule({
    imports: [CommissionRateEditorDialogComponent],
    providers: [
      { provide: MatDialogRef, useValue: dialogRef },
      { provide: MAT_DIALOG_DATA, useValue: { rate: options.rate ?? null } },
      { provide: AdminCommissionRatesDataService, useValue: { create, update } },
      { provide: ConfirmService, useValue: { confirm } },
      provideTransloco({
        config: { availableLangs: ['en', 'ne'], defaultLang: 'en' },
        loader: TestLoader,
      }),
    ],
  });

  const fixture = TestBed.createComponent(CommissionRateEditorDialogComponent);
  fixture.detectChanges();
  return {
    fixture,
    component: fixture.componentInstance,
    close,
    create,
    update,
    confirm,
    backdrop,
  };
}

function fillValidTier(component: CommissionRateEditorDialogComponent): void {
  component.form.patchValue({ ratePercent: 2 });
}

describe('CommissionRateEditorDialogComponent', () => {
  it('omits effectiveFrom on create when the field is left empty', async () => {
    const { component, create } = setup();
    fillValidTier(component);

    await component.submit();

    expect(create).toHaveBeenCalledTimes(1);
    const request = create.mock.calls[0]?.[0] as CommissionRateRequest;
    expect(request.effectiveFrom).toBeUndefined();
    expect(request.maxPrice).toBeNull();
  });

  it('sends the stored instant unchanged when the admin does not touch effectiveFrom', async () => {
    const stored = '2026-11-01T10:00:30.500Z';
    const { component, update } = setup({
      rate: {
        id: '1',
        version: 'v1',
        maxPrice: 100,
        maxInclusive: false,
        ratePercent: 2,
        isActive: true,
        effectiveFrom: stored,
      } as unknown as CommissionRateDto,
    });

    await component.submit();

    expect(update).toHaveBeenCalledWith(
      '1',
      expect.objectContaining({ effectiveFrom: stored }),
      'v1',
    );
  });

  it('rejects a non-positive max price without calling the API', async () => {
    const { component, create } = setup();
    component.form.patchValue({ maxPrice: 0, ratePercent: 2 });

    await component.submit();

    expect(create).not.toHaveBeenCalled();
    expect(component.form.controls.maxPrice.hasError('min')).toBe(true);
  });

  it('keeps a field error for a toggle inline, not in the banner', async () => {
    const { component, fixture, create } = setup({
      create: vi.fn<CreateFn>().mockRejectedValue(
        new ApiError({
          status: 400,
          message: 'Invalid.',
          fieldErrors: { maxInclusive: [{ code: 'x', message: 'Must include the maximum.' }] },
        }),
      ),
    });
    fillValidTier(component);

    await component.submit();
    fixture.detectChanges();

    expect(create).toHaveBeenCalled();
    expect(component.form.controls.maxInclusive.errors).toEqual({
      server: 'Must include the maximum.',
    });
    expect(component.banner()).toEqual([]);
    const toggle = (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>(
      '#maxInclusive-error',
    );
    expect(toggle?.textContent?.trim()).toBe('Must include the maximum.');
  });

  it('lists unattached messages in the banner and adds the related-tiers hint for a field-less conflict', async () => {
    const { component, fixture } = setup({
      create: vi.fn<CreateFn>().mockRejectedValue(
        new ApiError({
          status: 409,
          message: 'Conflicts with an existing tier.',
          relatedIds: ['0190a1'],
        }),
      ),
    });
    fillValidTier(component);

    await component.submit();
    fixture.detectChanges();

    expect(component.banner()).toEqual(['Conflicts with an existing tier.']);
    expect(component.relatedHint()).toBe(true);
    const text = (fixture.nativeElement as HTMLElement).querySelector('.action-error')?.textContent;
    expect(text).toContain('Conflicts with an existing tier.');
    expect(text).toContain('admin.relatedTiers');
  });

  it('does not show the related-tiers hint for a field error', async () => {
    const { component } = setup({
      create: vi.fn<CreateFn>().mockRejectedValue(
        new ApiError({
          status: 409,
          message: 'Conflict.',
          relatedIds: ['0190a1'],
          fieldErrors: { isActive: [{ code: 'x', message: 'Cannot deactivate.' }] },
        }),
      ),
    });
    fillValidTier(component);

    await component.submit();

    expect(component.relatedHint()).toBe(false);
    expect(component.banner()).toEqual(['Cannot deactivate.']);
  });

  it('asks before discarding edits when the backdrop is clicked on a dirty form', async () => {
    const { component, close, confirm, backdrop, fixture } = setup();
    fixture.detectChanges();
    component.form.controls.ratePercent.setValue(3);
    component.form.markAsDirty();

    backdrop.next(new MouseEvent('click'));

    await vi.waitFor(() => expect(confirm).toHaveBeenCalled());
    expect(close).not.toHaveBeenCalled();
  });
});
