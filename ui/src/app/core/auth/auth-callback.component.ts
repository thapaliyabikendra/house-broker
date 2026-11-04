import type { OnInit } from '@angular/core';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from './auth.service';

@Component({
  selector: 'hb-auth-callback',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe],
  template: `
    @if (failed()) {
      <section class="auth-callback" role="alert" aria-live="assertive">
        <p>{{ 'authCallback.error' | transloco }}</p>
        <button type="button" (click)="retry()">{{ 'authCallback.retry' | transloco }}</button>
      </section>
    } @else {
      <section class="auth-callback" aria-live="polite">
        <p>{{ 'authCallback.working' | transloco }}</p>
      </section>
    }
  `,
  styles: [
    `
      .auth-callback {
        padding: 2rem;
        text-align: center;
      }
    `,
  ],
})
export class AuthCallbackComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly failed = signal(false);

  async ngOnInit(): Promise<void> {
    const ok = await this.auth.handleCallback(window.location.search);
    if (!ok) {
      this.failed.set(true);
      return;
    }
    void this.router.navigateByUrl(this.auth.consumeReturnUrl());
  }

  retry(): void {
    void this.router.navigateByUrl('/');
    void this.auth.signIn();
  }
}
