import { FormControl, FormGroup } from '@angular/forms';
import { ApiError } from '../../core/http/api-error';
import { applyServerErrors } from './apply-server-errors';

const FALLBACK = 'Save failed.';

function buildForm() {
  return new FormGroup({
    minPrice: new FormControl<number | null>(0),
    minInclusive: new FormControl<boolean>(true, { nonNullable: true }),
    ratePercent: new FormControl<number | null>(2),
    isActive: new FormControl<boolean>(true, { nonNullable: true }),
  });
}

function apiError(fieldErrors: ApiError['fieldErrors'], message = 'Problem title'): ApiError {
  return new ApiError({ status: 400, message, fieldErrors });
}

describe('applyServerErrors', () => {
  it('attaches every message for a field as a server error, joined, and marks the control touched', () => {
    const form = buildForm();

    const unattached = applyServerErrors(
      form,
      apiError({
        minPrice: [
          { code: 'a', message: 'Must be zero or more.' },
          { code: 'b', message: 'Must be a whole amount.' },
        ],
      }),
      { inlineFields: ['minPrice'], fallbackMessage: FALLBACK },
    );

    expect(unattached).toEqual([]);
    expect(form.controls.minPrice.errors).toEqual({
      server: 'Must be zero or more. Must be a whole amount.',
    });
    expect(form.controls.minPrice.touched).toBe(true);
  });

  it('returns messages for unknown fields and for fields without an inline slot', () => {
    const form = buildForm();

    const unattached = applyServerErrors(
      form,
      apiError({
        unknownField: [{ code: 'x', message: 'Unknown field problem.' }],
        minInclusive: [{ code: 'y', message: 'Toggle problem.' }],
        ratePercent: [{ code: 'z', message: 'Rate problem.' }],
      }),
      { inlineFields: ['ratePercent'], fallbackMessage: FALLBACK },
    );

    expect(unattached).toEqual(['Unknown field problem.', 'Toggle problem.']);
    expect(form.controls.minInclusive.errors).toBeNull();
    expect(form.controls.minInclusive.touched).toBe(false);
    expect(form.controls.ratePercent.errors).toEqual({ server: 'Rate problem.' });
  });

  it('attaches an inline toggle when the caller lists it as inline', () => {
    const form = buildForm();

    const unattached = applyServerErrors(
      form,
      apiError({ minInclusive: [{ code: 'y', message: 'Toggle problem.' }] }),
      { inlineFields: ['minInclusive'], fallbackMessage: FALLBACK },
    );

    expect(unattached).toEqual([]);
    expect(form.controls.minInclusive.errors).toEqual({ server: 'Toggle problem.' });
  });

  it('returns the problem message when the failure has no field errors', () => {
    const form = buildForm();

    const unattached = applyServerErrors(form, apiError(undefined, 'Conflicts with a tier.'), {
      inlineFields: ['minPrice'],
      fallbackMessage: FALLBACK,
    });

    expect(unattached).toEqual(['Conflicts with a tier.']);
    expect(form.controls.minPrice.errors).toBeNull();
  });

  it('uses the fallback message for a failure that is not an Error', () => {
    const form = buildForm();

    const unattached = applyServerErrors(form, 'network down', {
      inlineFields: ['minPrice'],
      fallbackMessage: FALLBACK,
    });

    expect(unattached).toEqual([FALLBACK]);
  });

  it('ignores field entries with empty messages', () => {
    const form = buildForm();

    const unattached = applyServerErrors(
      form,
      apiError({ minPrice: [{ code: 'a', message: '' }] }),
      { inlineFields: ['minPrice'], fallbackMessage: FALLBACK },
    );

    expect(unattached).toEqual([]);
    expect(form.controls.minPrice.errors).toBeNull();
  });
});
