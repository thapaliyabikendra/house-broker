import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { TranslocoService } from '@jsverse/transloco';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { ConfirmDialogComponent } from './confirm-dialog.component';
import { ConfirmService } from './confirm.service';

describe('ConfirmService', () => {
  function setup(closeResult: boolean | undefined) {
    const open = vi.fn(() => ({ afterClosed: () => of(closeResult) }));
    TestBed.configureTestingModule({
      providers: [
        { provide: MatDialog, useValue: { open } },
        { provide: TranslocoService, useValue: { translate: (key: string) => `[${key}]` } },
      ],
    });
    return { service: TestBed.inject(ConfirmService), open };
  }

  it('resolves true when the user confirms', async () => {
    const { service } = setup(true);

    await expect(
      service.confirm({ message: 'Delete?', confirmLabel: 'Delete', destructive: true }),
    ).resolves.toBe(true);
  });

  it('resolves false when the dialog is dismissed without a choice', async () => {
    const { service } = setup(undefined);

    await expect(service.confirm({ message: 'Archive?', confirmLabel: 'Archive' })).resolves.toBe(
      false,
    );
  });

  it('opens an alertdialog with the translated cancel label and destructive flag', async () => {
    const { service, open } = setup(false);

    await service.confirm({ message: 'Delete?', confirmLabel: 'Delete', destructive: true });

    expect(open).toHaveBeenCalledWith(
      ConfirmDialogComponent,
      expect.objectContaining({
        role: 'alertdialog',
        data: {
          message: 'Delete?',
          confirmLabel: 'Delete',
          cancelLabel: '[common.cancel]',
          destructive: true,
        },
      }),
    );
  });
});
