import { Component, ChangeDetectionStrategy, input, output } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'hb-error-state',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatCardModule, MatButtonModule],
  template: `
    <mat-card appearance="outlined" class="error-state" role="alert">
      <mat-card-content>
        <h2 class="mat-title-medium">{{ title() }}</h2>
        @if (description()) {
          <p class="mat-body-medium">{{ description() }}</p>
        }
        @if (actionLabel()) {
          <button mat-stroked-button color="warn" type="button" (click)="retry.emit()">
            {{ actionLabel() }}
          </button>
        }
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    .error-state {
      text-align: center;
      padding: 2rem;
    }
    h2 {
      margin: 0 0 0.5rem;
    }
  `,
})
export class ErrorStateComponent {
  readonly title = input.required<string>();
  readonly description = input<string>();
  readonly actionLabel = input<string>();
  readonly retry = output<void>();
}
