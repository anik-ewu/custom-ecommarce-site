import { ViewportScroller } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterOutlet, Scroll } from '@angular/router';
import { filter } from 'rxjs';
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
export class App {
  constructor() {
    // The router scrolls to #fragments itself and ignores CSS scroll-margin, so offset it by the sticky
    // header's real height (taller on phones, where search sits on its own row).
    inject(ViewportScroller).setOffset(() => {
      const header = document.querySelector('app-site-header')?.getBoundingClientRect().height ?? 0;
      return [0, Math.ceil(header) + 16];
    });

    // Scrolling to a #fragment doesn't move keyboard focus; move it too, so the next Tab continues from the
    // section the shopper jumped to instead of from the link list.
    inject(Router)
      .events.pipe(
        filter((e): e is Scroll => e instanceof Scroll && !!e.anchor),
        takeUntilDestroyed(),
      )
      .subscribe((e) => {
        const target = document.getElementById(e.anchor!);
        if (!target) return;
        if (!target.hasAttribute('tabindex')) target.setAttribute('tabindex', '-1');
        target.focus({ preventScroll: true });
      });
  }
}
