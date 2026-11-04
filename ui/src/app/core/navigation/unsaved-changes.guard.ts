import { inject } from '@angular/core';
import type { CanDeactivateFn } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { ConfirmService } from '../../shared/ui/confirm.service';

/** Implemented by pages whose form can hold edits the user has not saved. */
export interface HasUnsavedChanges {
  hasUnsavedChanges(): boolean;
}

/** Asks before leaving a page with unsaved edits; leaving without edits is never blocked. */
export const unsavedChangesGuard: CanDeactivateFn<HasUnsavedChanges> = (component) => {
  if (!component.hasUnsavedChanges()) {
    return true;
  }
  const transloco = inject(TranslocoService);
  return inject(ConfirmService).confirm({
    message: transloco.translate('broker.editor.discardConfirm'),
    confirmLabel: transloco.translate('broker.editor.discard'),
    destructive: true,
  });
};
