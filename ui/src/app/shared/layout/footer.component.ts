import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { ClockService } from '../../core/time/clock.service';

@Component({
  selector: 'hb-footer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe],
  template: `
    <footer class="shell-footer">
      <section class="brand-block">
        <p class="brand">{{ 'title' | transloco }}</p>
        <p class="tagline">{{ 'footer.tagline' | transloco }}</p>
      </section>

      <div class="footer-bottom">
        <p class="copyright">{{ 'footer.copyright' | transloco: { year: year } }}</p>
        <a class="privacy-link" href="/sso/Privacy">{{ 'footer.privacy' | transloco }}</a>
      </div>
    </footer>
  `,
  styles: `
    .shell-footer {
      margin-block-start: 2rem;
      padding: 2rem 1rem 1.25rem;
      background: var(--mat-sys-surface-container-low);
      color: var(--mat-sys-on-surface);
      border-block-start: 1px solid var(--mat-sys-outline-variant);
    }
    .brand-block,
    .footer-bottom {
      max-inline-size: 1100px;
      margin-inline: auto;
    }
    .brand {
      margin: 0 0 0.25rem;
      font-size: 1.1rem;
      font-weight: 600;
    }
    .tagline,
    .copyright {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
    }
    .footer-bottom {
      display: flex;
      flex-wrap: wrap;
      justify-content: space-between;
      align-items: center;
      gap: 0.5rem 1rem;
      margin-block-start: 1.5rem;
      padding-block-start: 1rem;
      border-block-start: 1px solid var(--mat-sys-outline-variant);
      font-size: 0.875rem;
    }
    a {
      color: var(--mat-sys-primary);
    }
    a:focus-visible {
      outline: 2px solid var(--mat-sys-primary);
      outline-offset: 2px;
    }
  `,
})
export class FooterComponent {
  private readonly clock = inject(ClockService);
  readonly year = this.clock.now().getFullYear();
}
