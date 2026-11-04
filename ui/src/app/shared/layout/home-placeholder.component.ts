import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'hb-home-placeholder',
  standalone: true,
  imports: [RouterLink],
  template: `
    <section>
      <h2>Welcome</h2>
      @if (auth.user(); as user) {
        <p>Signed in as {{ user.name ?? user.sub }}</p>
      } @else {
        <p>
          <button type="button" (click)="auth.signIn()">Sign in</button>
        </p>
      }
      <p><a routerLink="/search">Search listings</a></p>
    </section>
  `,
})
export class HomePlaceholderComponent {
  readonly auth = inject(AuthService);
}
