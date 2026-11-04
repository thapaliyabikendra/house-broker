import type { OnInit } from '@angular/core';
import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';

@Component({
  selector: 'hb-auth-callback',
  standalone: true,
  template: '<p>Completing sign in…</p>',
})
export class AuthCallbackComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  async ngOnInit(): Promise<void> {
    const ok = await this.auth.handleCallback(window.location.search);
    const target = ok ? '/' : '/';
    void this.router.navigateByUrl(target);
  }
}
