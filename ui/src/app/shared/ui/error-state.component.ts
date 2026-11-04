import { Component, ChangeDetectionStrategy, input, output } from '@angular/core';

@Component({
  selector: 'hb-error-state',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="error-state" role="alert">
      <h2>{{ title() }}</h2>
      @if (description()) {
        <p>{{ description() }}</p>
      }
      @if (actionLabel()) {
        <button type="button" (click)="retry.emit()">{{ actionLabel() }}</button>
      }
    </div>
  `,
  styles: `
    .error-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.5rem;
      padding: 2rem;
      text-align: center;
    }
  `,
})
export class ErrorStateComponent {
  readonly title = input.required<string>();
  readonly description = input<string>();
  readonly actionLabel = input<string>();
  readonly retry = output<void>();
}
