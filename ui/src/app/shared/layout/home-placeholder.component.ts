import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'hb-home-placeholder',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslocoPipe],
  template: `
    <section>
      <h2>{{ 'home.welcome' | transloco }}</h2>
      @if (auth.user(); as user) {
        <p>{{ 'home.signedInAs' | transloco: { name: user.name ?? user.sub } }}</p>
      } @else {
        <p>
          <button type="button" (click)="auth.signIn()">{{ 'header.signIn' | transloco }}</button>
        </p>
      }
      <p>
        <a routerLink="/search">{{ 'search.title' | transloco }}</a>
      </p>
    </section>
  `,
})
export class HomePlaceholderComponent {
  readonly auth = inject(AuthService);
}
