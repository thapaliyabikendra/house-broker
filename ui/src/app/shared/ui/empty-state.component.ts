import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { MatCardModule } from '@angular/material/card';

@Component({
  selector: 'hb-empty-state',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatCardModule],
  template: `
    <mat-card appearance="outlined" class="empty-state" role="status">
      <mat-card-content>
        <h2 class="mat-headline-small">{{ title() }}</h2>
        @if (description()) {
          <p class="mat-body-medium">{{ description() }}</p>
        }
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    .empty-state {
      text-align: center;
      padding: 2rem;
    }
    h2 {
      margin: 0 0 0.5rem;
    }
  `,
})
export class EmptyStateComponent {
  readonly title = input.required<string>();
  readonly description = input<string>();
}
