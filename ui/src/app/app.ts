import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { HeaderComponent } from './shared/layout/header.component';

@Component({
  imports: [RouterOutlet, HeaderComponent],
  selector: 'hb-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {
  protected readonly title = signal('house-broker');
}
