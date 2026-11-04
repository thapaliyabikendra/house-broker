/** Controls that carry Angular's `ng-invalid` class when their form control is invalid. */
const INVALID_CONTROL_SELECTOR = 'input.ng-invalid, textarea.ng-invalid, mat-select.ng-invalid';

/**
 * Moves focus to the first invalid control inside `root`, so a rejected submit
 * takes the keyboard user to the problem (WCAG 3.3.1, plan 4.10).
 */
export function focusFirstInvalid(root: HTMLElement): void {
  root.querySelector<HTMLElement>(INVALID_CONTROL_SELECTOR)?.focus();
}
