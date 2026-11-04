import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

@Component({
  selector: 'hb-loading-state',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatProgressSpinnerModule],
  template: `
    <div class="loading-state" role="status" aria-live="polite">
      <mat-spinner diameter="40" />
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
  `,
})
export class LoadingStateComponent {
  readonly label = input<string>();
}
