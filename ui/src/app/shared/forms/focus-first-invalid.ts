/**
 * Controls that carry Angular's `ng-invalid` class when their form control is invalid. A slide
 * toggle's focusable element is its inner switch button, so that is what gets focused.
 */
const INVALID_CONTROL_SELECTOR = [
  'input.ng-invalid',
  'textarea.ng-invalid',
  'mat-select.ng-invalid',
  'mat-slide-toggle.ng-invalid button[role="switch"]',
].join(', ');

/**
 * Moves focus to the first invalid control inside `root`, so a rejected submit
 * takes the keyboard user to the problem (WCAG 3.3.1, plan 4.10).
 */
export function focusFirstInvalid(root: HTMLElement): void {
  root.querySelector<HTMLElement>(INVALID_CONTROL_SELECTOR)?.focus();
}
