import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

@Component({
  selector: 'hb-home-placeholder',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    TranslocoPipe,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  template: `
    <section class="hero" aria-labelledby="hero-title">
      <h1 id="hero-title">{{ 'home.heroTitle' | transloco }}</h1>
      <p class="lead">{{ 'home.heroLead' | transloco }}</p>

      <form class="hero-search" role="search" [formGroup]="form" (ngSubmit)="search()">
        <mat-form-field appearance="outline">
          <mat-label>{{ 'search.location' | transloco }}</mat-label>
          <input matInput formControlName="location" />
        </mat-form-field>
        <button mat-raised-button color="primary" type="submit">
          {{ 'search.submit' | transloco }}
        </button>
      </form>

      <p class="secondary-actions">
        <a mat-stroked-button routerLink="/search">{{ 'home.browseAll' | transloco }}</a>
        @if (auth.user(); as user) {
          <span>{{
            'home.signedInAs' | transloco: { name: user.name ?? ('header.account' | transloco) }
          }}</span>
        } @else {
          <button mat-button type="button" (click)="signIn()">
            {{ 'header.signIn' | transloco }}
          </button>
        }
      </p>
    </section>
  `,
  styles: `
    .hero {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 1rem;
      padding-block: 3rem 2rem;
      text-align: center;
    }
    h1 {
      margin: 0;
    }
    .lead {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
    }
    .hero-search {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      inline-size: 100%;
      max-inline-size: 36rem;
    }
    .hero-search mat-form-field {
      flex: 1 1 auto;
    }
    .secondary-actions {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      margin: 0;
    }
  `,
})
export class HomePlaceholderComponent {
  private readonly router = inject(Router);
  readonly auth = inject(AuthService);

  readonly form = new FormGroup({
    location: new FormControl<string>('', { nonNullable: true }),
  });

  search(): void {
    const location = this.form.getRawValue().location.trim();
    void this.router.navigate(['/search'], {
      queryParams: { location: location || undefined },
    });
  }

  signIn(): void {
    void this.auth.signIn(undefined, this.router.url);
  }
}
