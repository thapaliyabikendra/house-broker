import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'hb-home-placeholder',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslocoPipe, MatCardModule, MatButtonModule],
  template: `
    <section class="home">
      <mat-card appearance="outlined" class="hero">
        <mat-card-header>
          <mat-card-title>{{ 'home.welcome' | transloco }}</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          @if (auth.user(); as user) {
            <p>{{ 'home.signedInAs' | transloco: { name: user.name ?? user.sub } }}</p>
          } @else {
            <p>
              <button mat-raised-button color="primary" type="button" (click)="signIn()">
                {{ 'header.signIn' | transloco }}
              </button>
            </p>
          }
          <p>
            <a mat-stroked-button routerLink="/search">{{ 'search.title' | transloco }}</a>
          </p>
        </mat-card-content>
      </mat-card>
    </section>
  `,
  styles: `
    .home {
      display: flex;
      justify-content: center;
      padding-block-start: 2rem;
    }
    .hero {
      max-inline-size: 32rem;
      width: 100%;
    }
  `,
})
export class HomePlaceholderComponent {
  private readonly router = inject(Router);
  readonly auth = inject(AuthService);

  signIn(): void {
    void this.auth.signIn(undefined, this.router.url);
  }
}
