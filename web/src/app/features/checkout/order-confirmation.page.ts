import { HttpErrorResponse, httpResource } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TakaPipe } from '../../shared/money/taka.pipe';
import { Order } from './checkout.models';

@Component({
  selector: 'app-order-confirmation-page',
  imports: [RouterLink, TakaPipe],
  template: `
    <section class="container confirm" aria-labelledby="confirm-title">
      @if (order.hasValue()) {
        @let o = order.value();
        <p class="eyebrow muted">Order {{ o.number }}</p>
        <h1 id="confirm-title" tabindex="-1">
          Thank you, {{ o.shippingAddress.fullName.split(' ')[0] }}.
        </h1>
        <p class="lead">
          Your order is placed. We'll call {{ o.shippingAddress.phone }} to confirm, then deliver in
          3–5 days. You pay {{ o.total | taka }} in cash when it arrives.
        </p>

        <div class="confirm__card">
          <ul class="items">
            @for (item of o.items; track item.productId) {
              <li>
                <span
                  >{{ item.name }}
                  @if (item.quantity > 1) {
                    <span class="muted">× {{ item.quantity }}</span>
                  }
                </span>
                <span>{{ item.lineTotal | taka }}</span>
              </li>
            }
          </ul>
          <dl class="totals">
            <div>
              <dt>Subtotal</dt>
              <dd>{{ o.subtotal | taka }}</dd>
            </div>
            <div>
              <dt>Delivery</dt>
              <dd>{{ o.shippingFee === 0 ? 'Free' : (o.shippingFee | taka) }}</dd>
            </div>
            <div class="totals__total">
              <dt>Total, cash on delivery</dt>
              <dd>{{ o.total | taka }}</dd>
            </div>
          </dl>
          <div class="address">
            <h2>Delivering to</h2>
            <p>
              {{ o.shippingAddress.fullName }}<br />
              {{ o.shippingAddress.line1 }}<br />
              @if (o.shippingAddress.line2) {
                {{ o.shippingAddress.line2 }}<br />
              }
              {{ o.shippingAddress.city }} {{ o.shippingAddress.postalCode }}
            </p>
          </div>
        </div>

        <a class="btn btn--primary" routerLink="/shop">Continue shopping</a>
      } @else if (notFound()) {
        <h1 id="confirm-title">We couldn't find that order</h1>
        <a class="btn btn--primary" routerLink="/shop">Explore the collection</a>
      } @else if (order.error()) {
        <h1 id="confirm-title">Something went wrong</h1>
        <button class="btn btn--ghost" type="button" (click)="order.reload()">Try again</button>
      } @else {
        <p class="muted" aria-busy="true">Loading your order…</p>
      }
    </section>
  `,
  styles: `
    .confirm {
      display: grid;
      justify-items: start;
      gap: var(--space-4);
      max-width: 44rem;
      padding-block: var(--space-8);
    }
    .confirm h1:focus {
      outline: none;
    }
    .eyebrow {
      margin: 0;
      font-size: var(--text-sm);
      letter-spacing: 0.06em;
      text-transform: uppercase;
    }
    .lead {
      font-size: var(--text-lg);
    }
    .confirm__card {
      display: grid;
      gap: var(--space-5);
      width: 100%;
      padding: var(--space-5);
      border: var(--stitch-weight) dashed var(--color-stitch);
      border-radius: var(--radius-lg);
      background: var(--color-surface);
    }
    .items {
      display: grid;
      gap: var(--space-2);
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .items li,
    .totals div {
      display: flex;
      justify-content: space-between;
      gap: var(--space-3);
    }
    .totals {
      display: grid;
      gap: var(--space-2);
      margin: 0;
    }
    .totals dd {
      margin: 0;
    }
    .totals__total {
      font-weight: var(--weight-semibold);
    }
    .address h2 {
      margin: 0 0 var(--space-2);
      font-size: var(--text-lg);
    }
    .address p {
      margin: 0;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderConfirmationPage {
  readonly number = input.required<string>();

  protected readonly order = httpResource<Order>(
    () => `/api/orders/${encodeURIComponent(this.number())}`,
  );
  protected readonly notFound = computed(
    () => (this.order.error() as HttpErrorResponse | undefined)?.status === 404,
  );

  constructor() {
    const injector = inject(Injector);
    // Arriving from checkout, focus the thank-you heading so screen readers start at the confirmation.
    effect(() => {
      if (this.order.hasValue()) {
        afterNextRender(() => document.getElementById('confirm-title')?.focus(), { injector });
      }
    });
  }
}
