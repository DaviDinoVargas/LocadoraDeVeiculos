import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { LogoutWidgetComponent } from "./auth/logout-widget.component";

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, LogoutWidgetComponent],
  templateUrl: './app.html',
  styleUrls: ['./app.scss']
})
export class App {
  protected readonly title = signal('client-app');
}
