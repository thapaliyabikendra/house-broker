import { Component, ChangeDetectionStrategy } from '@angular/core';

@Component({
  selector: 'hb-footer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <footer class="shell-footer">
      <p>© House Broker</p>
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
