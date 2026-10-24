import { DatePipe } from '@angular/common';
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
import { SHOP_INFO } from '../../core/shop-info';
import { TakaPipe } from '../../shared/money/taka.pipe';
import { Order } from '../checkout/checkout.models';
import { ORDER_STEPS, orderStatusLabel } from './order-status';

/** Order detail. Straight after checkout (router state `placed`) it doubles as the thank-you page. */
@Component({
  selector: 'app-order-page',
  imports: [RouterLink, TakaPipe, DatePipe],
  templateUrl: './order.page.html',
  styleUrl: './order.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderPage {
  readonly number = input.required<string>();

  // Read once: history.state survives a reload of the same entry, which is fine for a thank-you message.
  protected readonly justPlaced = !!(history.state as { placed?: boolean } | null)?.placed;
  protected readonly steps = ORDER_STEPS;
  protected readonly shop = SHOP_INFO;
  protected readonly statusLabel = orderStatusLabel;

  protected readonly order = httpResource<Order>(
    () => `/api/orders/${encodeURIComponent(this.number())}`,
  );
  protected readonly notFound = computed(
    () => (this.order.error() as HttpErrorResponse | undefined)?.status === 404,
  );
  protected readonly stepIndex = computed(() =>
    this.order.hasValue()
      ? ORDER_STEPS.indexOf(this.order.value().status as (typeof ORDER_STEPS)[number])
      : -1,
  );

  constructor() {
    const injector = inject(Injector);
    // Focus the heading once loaded so screen readers start at the order (e.g. arriving from checkout).
    effect(() => {
      if (this.order.hasValue()) {
        afterNextRender(() => document.getElementById('order-title')?.focus(), { injector });
      }
    });
  }
}
