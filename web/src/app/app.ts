import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SiteFooter } from './core/layout/site-footer';
import { SiteHeader } from './core/layout/site-header';
import { CartDrawer } from './features/cart/cart-drawer';
import { ToastHost } from './shared/toast/toast-host';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, SiteHeader, SiteFooter, ToastHost, CartDrawer],
  templateUrl: './app.html',
  styleUrl: './app.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {}
