import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'hb-footer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe],
  template: `
    <footer class="shell-footer">
      <p>{{ 'footer.text' | transloco }}</p>
    </footer>
  `,
  styles: `
    .shell-footer {
      padding: 1rem;
      text-align: center;
    }
  `,
})
export class FooterComponent {}
