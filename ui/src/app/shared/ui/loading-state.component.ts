import { Component, ChangeDetectionStrategy, input } from '@angular/core';

@Component({
  selector: 'hb-loading-state',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="loading-state" role="status" aria-live="polite">
      <span class="spinner" aria-hidden="true"></span>
      @if (label()) {
        <p>{{ label() }}</p>
      }
    </div>
  `,
  styles: `
    .loading-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.75rem;
      padding: 2rem;
    }
    .spinner {
      inline-size: 2rem;
      block-size: 2rem;
      border: 0.2rem solid currentColor;
      border-block-end-color: transparent;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }
    @keyframes spin {
      to {
        transform: rotate(360deg);
      }
    }
  `,
})
export class LoadingStateComponent {
  readonly label = input<string>();
}
