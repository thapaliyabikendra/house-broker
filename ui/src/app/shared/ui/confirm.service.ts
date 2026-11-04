import { inject, Injectable } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { ConfirmDialogComponent, type ConfirmDialogData } from './confirm-dialog.component';

export interface ConfirmOptions {
  /** Already-translated question shown as the dialog title. */
  message: string;
  /** Already-translated label for the confirming action. */
  confirmLabel: string;
  destructive?: boolean;
}

/** Opens the app's confirmation dialog in place of the blocking, unstyled `window.confirm`. */
@Injectable({ providedIn: 'root' })
export class ConfirmService {
  private readonly dialog = inject(MatDialog);
  private readonly transloco = inject(TranslocoService);

  /** Resolves `true` only when the user confirms; cancel, Escape and backdrop clicks resolve `false`. */
  async confirm(options: ConfirmOptions): Promise<boolean> {
    const data: ConfirmDialogData = {
      message: options.message,
      confirmLabel: options.confirmLabel,
      cancelLabel: this.transloco.translate('common.cancel'),
      destructive: options.destructive ?? false,
    };
    const result = await firstValueFrom(
      this.dialog
        .open<ConfirmDialogComponent, ConfirmDialogData, boolean>(ConfirmDialogComponent, {
          data,
          role: 'alertdialog',
          autoFocus: 'first-tabbable',
          restoreFocus: true,
        })
        .afterClosed(),
    );
    return result === true;
  }
}
