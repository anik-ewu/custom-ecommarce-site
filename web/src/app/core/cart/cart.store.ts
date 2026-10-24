import { HttpClient } from '@angular/common/http';
import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { Observable, firstValueFrom } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { toProblem } from '../http/problem';
import { ToastService } from '../../shared/toast/toast.service';
import { Cart } from './cart.models';

/** The shopper's cart. The server owns it (anonymous cookie or account); this is a cached copy. */
@Injectable({ providedIn: 'root' })
export class CartStore {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly toasts = inject(ToastService);

  readonly cart = signal<Cart | null>(null);
  readonly count = computed(() => this.cart()?.itemCount ?? 0);
  readonly drawerOpen = signal(false);
  readonly busy = signal(false);
  readonly loadError = signal(false);
  /** Name of the piece just added, so the drawer can say what happened. */
  readonly lastAdded = signal<string | null>(null);

  constructor() {
    // Signing in merges the anonymous cart on the server, and signing out switches carts: reload either way.
    effect(() => {
      this.auth.user();
      untracked(() => void this.load());
    });
  }

  holds(productId: string): boolean {
    return this.heldUntil(productId) !== null;
  }

  heldUntil(productId: string): string | null {
    return this.cart()?.items.find((i) => i.productId === productId)?.reservedUntil ?? null;
  }

  async load(): Promise<void> {
    try {
      this.cart.set(await firstValueFrom(this.http.get<Cart>('/api/cart')));
      this.loadError.set(false);
    } catch {
      // The header count can wait; the cart page shows the error with a retry.
      this.loadError.set(true);
    }
  }

  /** Adds a piece and opens the drawer. Resolves false (after a toast) when the server refuses. */
  add(productId: string, quantity = 1): Promise<boolean> {
    return this.run(this.http.post<Cart>('/api/cart/items', { productId, quantity }), (cart) => {
      this.lastAdded.set(cart.items.find((i) => i.productId === productId)?.name ?? null);
      this.drawerOpen.set(true);
    });
  }

  update(itemId: string, quantity: number): Promise<boolean> {
    return this.run(this.http.patch<Cart>(`/api/cart/items/${itemId}`, { quantity }));
  }

  remove(itemId: string): Promise<boolean> {
    const name = this.cart()?.items.find((i) => i.id === itemId)?.name;
    return this.run(this.http.delete<Cart>(`/api/cart/items/${itemId}`), () =>
      this.toasts.show(name ? `Removed ${name}.` : 'Removed from your cart.'),
    );
  }

  // Buttons use aria-disabled rather than disabled while busy, so focus never drops; this guard does the blocking.
  private async run(request: Observable<Cart>, onSuccess?: (cart: Cart) => void): Promise<boolean> {
    if (this.busy()) return false;
    this.busy.set(true);
    try {
      const cart = await firstValueFrom(request);
      this.cart.set(cart);
      onSuccess?.(cart);
      return true;
    } catch (e) {
      this.toasts.show(toProblem(e).title, 'error');
      return false;
    } finally {
      this.busy.set(false);
    }
  }
}
