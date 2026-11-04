import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ClockService {
  readonly timeZone = signal(this.resolveTimeZone());

  now(): Date {
    return new Date();
  }

  private resolveTimeZone(): string {
    try {
      return Intl.DateTimeFormat().resolvedOptions().timeZone || 'Asia/Kathmandu';
    } catch {
      return 'Asia/Kathmandu';
    }
  }
}
