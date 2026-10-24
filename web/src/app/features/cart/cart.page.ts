import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CartStore } from '../../core/cart/cart.store';
import { TakaPipe } from '../../shared/money/taka.pipe';
import { CartLines } from './cart-lines';

@Component({
  selector: 'app-cart-page',
  imports: [RouterLink, TakaPipe, CartLines],
  template: `
    <section class="container cart" aria-labelledby="cart-title">
      <h1 id="cart-title" tabindex="-1">Your cart</h1>

      @if (store.cart(); as cart) {
        @if (cart.items.length === 0) {
          <div class="empty">
            <p class="muted">
              Nothing here yet. One-of-a-kind pieces are held for 15 minutes once you add them.
            </p>
            <a class="btn btn--primary" routerLink="/shop">Explore the collection</a>
          </div>
        } @else {
          <div class="cart__layout">
            <app-cart-lines [items]="cart.items" emptyFocusId="cart-title" />
            <aside class="summary" aria-label="Order summary">
              <p class="summary__row">
                <span>Subtotal</span><strong>{{ cart.subtotal | taka }}</strong>
              </p>
              <p class="muted summary__note">
                Delivery is calculated at checkout. Cash on delivery available.
              </p>
              <a class="btn btn--primary btn--block" routerLink="/checkout">Checkout</a>
              <a class="btn btn--ghost btn--block" routerLink="/shop">Keep browsing</a>
            </aside>
          </div>
        }
      } @else if (store.loadError()) {
        <div class="empty">
          <p class="muted">We couldn't load your cart. Please check your connection.</p>
          <button class="btn btn--ghost" type="button" (click)="store.load()">Try again</button>
        </div>
      } @else {
        <p class="muted" aria-busy="true">Loading your cart…</p>
      }
    </section>
  `,
  styles: `
    .cart {
      padding-block: var(--space-6) var(--space-8);
    }
    .cart__layout {
      display: grid;
      gap: var(--space-6);
    }
    @media (min-width: 56rem) {
      .cart__layout {
        grid-template-columns: 1fr 20rem;
        gap: var(--space-8);
        align-items: start;
      }
    }
    .summary {
      display: grid;
      gap: var(--space-3);
      padding: var(--space-5);
      border-radius: var(--radius-lg);
      background: var(--color-surface);
    }
    .summary__row {
      display: flex;
      justify-content: space-between;
      margin: 0;
      font-size: var(--text-lg);
    }
    .summary__note {
      margin: 0;
      font-size: var(--text-sm);
    }
    .empty {
      display: grid;
      justify-items: start;
      gap: var(--space-3);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CartPage {
  protected readonly store = inject(CartStore);

  constructor() {
    void this.store.load();
  }
}
