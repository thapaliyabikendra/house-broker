import type { AbstractControl } from '@angular/forms';
import { ApiError } from '../../core/http/api-error';

export interface ApplyServerErrorsOptions {
  /** Form fields that have a visible inline error slot. Other fields are reported back instead. */
  inlineFields: readonly string[];
  /** Banner text for a failure that is not an Error (for example, a thrown string). */
  fallbackMessage: string;
}

/**
 * Attaches a failed request's field errors to their controls as `{ server: message }`, with all
 * messages for a field joined. Controls are marked touched so their inline errors show.
 *
 * Returns the messages that could not be shown inline (unknown fields, fields without an inline
 * slot, or a failure with no field detail) so the caller can list them in its banner. A message
 * is never both attached and returned, which keeps the banner free of duplicates.
 */
export function applyServerErrors(
  form: AbstractControl,
  error: unknown,
  options: ApplyServerErrorsOptions,
): string[] {
  if (!(error instanceof ApiError) || !error.fieldErrors) {
    return [error instanceof Error ? error.message : options.fallbackMessage];
  }

  const unattached: string[] = [];
  for (const [field, entries] of Object.entries(error.fieldErrors)) {
    const messages = entries.map((entry) => entry.message).filter((message) => message.length > 0);
    if (messages.length === 0) {
      continue;
    }
    const control = options.inlineFields.includes(field) ? form.get(field) : null;
    if (control) {
      control.setErrors({ server: messages.join(' ') });
      control.markAsTouched();
    } else {
      unattached.push(...messages);
    }
  }
  return unattached;
}
