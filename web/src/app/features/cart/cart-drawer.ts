import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  effect,
  inject,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { CartStore } from '../../core/cart/cart.store';
import { TakaPipe } from '../../shared/money/taka.pipe';
import { CartLines } from './cart-lines';

/** Slide-in cart. A native modal <dialog> gives focus trapping, Escape to close and inert background for free. */
@Component({
  selector: 'app-cart-drawer',
  // Backdrop clicks bubble up from the <dialog>; keyboard users close with Escape or the close button.
  host: { '(click)': 'closeOnBackdrop($event)' },
  imports: [RouterLink, TakaPipe, CartLines],
  template: `
    <dialog #dialog class="drawer" aria-labelledby="drawer-title" (close)="onClosed()">
      <div class="drawer__panel">
        <header class="drawer__head">
          <h2 id="drawer-title" tabindex="-1">Your cart</h2>
          <button class="drawer__close" type="button" (click)="close()" aria-label="Close cart">
            ×
          </button>
        </header>

        <div class="drawer__body">
          @if (store.cart(); as cart) {
            @if (store.lastAdded(); as added) {
              <p class="drawer__added">Added: {{ added }}</p>
            }
            @if (cart.items.length === 0) {
              <p class="muted">Your cart is empty.</p>
            } @else {
              <app-cart-lines [items]="cart.items" emptyFocusId="drawer-title" />
              <p class="drawer__subtotal">
                <span>Subtotal</span><strong>{{ cart.subtotal | taka }}</strong>
              </p>
            }
          }
        </div>

        <div class="drawer__actions">
          @if (store.count() > 0) {
            <a class="btn btn--primary btn--block" routerLink="/checkout" (click)="close()"
              >Checkout</a
            >
            <a class="btn btn--ghost btn--block" routerLink="/cart" (click)="close()">View cart</a>
          } @else {
            <button class="btn btn--ghost btn--block" type="button" (click)="close()">
              Keep browsing
            </button>
          }
        </div>
      </div>
    </dialog>
  `,
  styles: `
    .drawer {
      width: min(100vw, 26rem);
      max-width: 100vw;
      height: 100dvh;
      max-height: 100dvh;
      margin: 0 0 0 auto;
      padding: 0;
      border: 0;
      background: var(--color-bg);
      color: var(--color-ink);
      box-shadow: var(--shadow-md);
    }
    .drawer[open] {
      animation: slide-in var(--duration-slow) var(--ease);
    }
    .drawer::backdrop {
      background: var(--color-overlay);
    }
    .drawer__panel {
      display: grid;
      grid-template-rows: auto 1fr auto;
      gap: var(--space-4);
      height: 100%;
      padding: var(--space-5);
      overflow-y: auto;
    }
    .drawer__head {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .drawer__head h2 {
      margin: 0;
      font-size: var(--text-xl);
    }
    .drawer__close {
      min-width: var(--tap-target);
      min-height: var(--tap-target);
      border: 0;
      background: none;
      color: inherit;
      font-size: var(--text-xl);
      cursor: pointer;
    }
    .drawer__body {
      display: grid;
      align-content: start;
      gap: var(--space-4);
    }
    .drawer__added {
      margin: 0;
      color: var(--color-success);
      font-size: var(--text-sm);
    }
    .drawer__subtotal {
      display: flex;
      justify-content: space-between;
      margin: 0;
    }
    .drawer__actions {
      display: grid;
      gap: var(--space-2);
    }
    @keyframes slide-in {
      from {
        transform: translateX(100%);
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CartDrawer {
  protected readonly store = inject(CartStore);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private returnFocus: HTMLElement | null = null;

  constructor() {
    effect(() => {
      const el = this.dialog().nativeElement;
      if (this.store.drawerOpen() && !el.open) {
        this.returnFocus = document.activeElement as HTMLElement | null;
        el.showModal();
      }
      if (!this.store.drawerOpen() && el.open) el.close();
    });
  }

  close(): void {
    this.store.drawerOpen.set(false);
  }

  /** Back to where the shopper was; if that control was replaced (e.g. Add became "In your cart"), to main. */
  onClosed(): void {
    this.store.drawerOpen.set(false);
    this.store.lastAdded.set(null);
    const target = this.returnFocus?.isConnected
      ? this.returnFocus
      : (document.getElementById('buy-box') ?? document.getElementById('main'));
    target?.focus();
    this.returnFocus = null;
  }

  closeOnBackdrop(event: MouseEvent): void {
    // Clicks on the ::backdrop land on the dialog element itself.
    if (event.target === this.dialog().nativeElement) this.close();
  }
}
