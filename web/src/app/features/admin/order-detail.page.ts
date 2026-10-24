import { DatePipe } from '@angular/common';
import { HttpClient, httpResource } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  afterNextRender,
  inject,
  input,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { toProblem } from '../../core/http/problem';
import { TakaPipe } from '../../shared/money/taka.pipe';
import { ToastService } from '../../shared/toast/toast.service';
import { orderStatusLabel } from '../account/order-status';
import { AdminOrder } from './admin.models';

const ACTION_LABELS: Record<string, string> = {
  Confirmed: 'Mark confirmed',
  Shipped: 'Mark shipped',
  Delivered: 'Mark delivered',
  Cancelled: 'Cancel order',
};

@Component({
  selector: 'app-admin-order-detail-page',
  imports: [RouterLink, TakaPipe, DatePipe],
  template: `
    <a class="back stitch-link" routerLink="/admin/orders">← All orders</a>

    @if (detail.hasValue()) {
      @let d = detail.value();
      @let o = d.order;
      <div class="page-head">
        <h1>{{ o.number }}</h1>
        <span class="pill">{{ statusLabel(o.status) }}</span>
      </div>
      <p class="muted">{{ o.createdAt | date: 'd MMM y, h:mm a' }} · {{ d.customerEmail }}</p>

      @if (d.nextStatuses.length && !confirmCancel()) {
        <div class="actions" role="group" aria-label="Update status">
          @for (next of d.nextStatuses; track next; let first = $first) {
            <button
              type="button"
              class="btn"
              [class.btn--primary]="first && next !== 'Cancelled'"
              [class.btn--ghost]="!first || next === 'Cancelled'"
              [attr.aria-disabled]="busy()"
              [attr.data-status]="next"
              (click)="move(next)"
            >
              {{ actionLabel(next) }}
            </button>
          }
        </div>
      }
      @if (confirmCancel()) {
        <div
          class="confirm"
          role="alertdialog"
          aria-labelledby="cancel-title"
          aria-describedby="cancel-desc"
        >
          <p id="cancel-title">
            <strong>Cancel {{ o.number }}?</strong>
          </p>
          <p id="cancel-desc" class="muted">
            Stock goes back and one-of-a-kind pieces become available again.
          </p>
          <button class="btn btn--danger" type="button" (click)="move('Cancelled', true)">
            Yes, cancel it
          </button>
          <button id="keep-order" class="btn btn--ghost" type="button" (click)="keepOrder()">
            Keep the order
          </button>
        </div>
      }

      <div class="grid">
        <section class="panel" aria-labelledby="items-title">
          <h2 id="items-title">Items</h2>
          <ul class="lines">
            @for (item of o.items; track item.productId) {
              <li>
                <span
                  >{{ item.name }} <span class="muted">× {{ item.quantity }}</span></span
                ><span>{{ item.lineTotal | taka }}</span>
              </li>
            }
          </ul>
          <dl class="lines">
            <div>
              <dt>Subtotal</dt>
              <dd>{{ o.subtotal | taka }}</dd>
            </div>
            <div>
              <dt>Delivery</dt>
              <dd>{{ o.shippingFee === 0 ? 'Free' : (o.shippingFee | taka) }}</dd>
            </div>
            <div class="total">
              <dt>
                Total ({{ o.paymentMethod === 'CashOnDelivery' ? 'cash on delivery' : 'card' }})
              </dt>
              <dd>{{ o.total | taka }}</dd>
            </div>
          </dl>
        </section>
        <section class="panel" aria-labelledby="ship-title">
          <h2 id="ship-title">Deliver to</h2>
          <p>
            {{ o.shippingAddress.fullName }}<br />
            <a class="order-tel" [href]="'tel:' + o.shippingAddress.phone">{{
              o.shippingAddress.phone
            }}</a
            ><br />
            {{ o.shippingAddress.line1 }}<br />
            @if (o.shippingAddress.line2) {
              {{ o.shippingAddress.line2 }}<br />
            }
            {{ o.shippingAddress.city }} {{ o.shippingAddress.postalCode }}
          </p>
        </section>
      </div>
    } @else if (detail.error()) {
      <p class="empty">That order didn't load.</p>
    } @else {
      <p class="muted" aria-busy="true">Loading…</p>
    }
  `,
  styleUrl: './admin.scss',
  styles: `
    .back {
      display: inline-flex;
      align-items: center;
      min-height: var(--tap-target);
      margin-block-end: var(--space-3);
      font-size: var(--text-sm);
    }
    .page-head {
      margin-block-end: var(--space-1);
    }
    .order-tel {
      display: inline-flex;
      align-items: center;
      min-height: var(--tap-target);
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-3);
      margin-block: var(--space-4);
    }
    .confirm {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--space-3);
      padding: var(--space-4);
      margin-block-end: var(--space-4);
      border: var(--stitch-weight) dashed var(--color-danger);
      border-radius: var(--radius-md);
    }
    .confirm p {
      flex-basis: 100%;
      margin: 0;
    }
    .grid {
      display: grid;
      gap: var(--space-4);
    }
    @media (min-width: 64rem) {
      .grid {
        grid-template-columns: 2fr 1fr;
        align-items: start;
      }
    }
    .lines {
      display: grid;
      gap: var(--space-2);
      margin: 0 0 var(--space-4);
      padding: 0;
      list-style: none;
    }
    .lines li,
    .lines div {
      display: flex;
      justify-content: space-between;
      gap: var(--space-3);
    }
    .lines dd {
      margin: 0;
    }
    .total {
      font-weight: var(--weight-semibold);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminOrderDetailPage {
  private readonly http = inject(HttpClient);
  private readonly toasts = inject(ToastService);

  readonly number = input.required<string>();
  protected readonly detail = httpResource<AdminOrder>(
    () => `/api/admin/orders/${encodeURIComponent(this.number())}`,
  );
  protected readonly busy = signal(false);
  protected readonly confirmCancel = signal(false);
  private readonly injector = inject(Injector);

  keepOrder(): void {
    this.confirmCancel.set(false);
    this.focusAfterRender('[data-status="Cancelled"]');
  }

  private focusAfterRender(selector: string): void {
    afterNextRender(() => document.querySelector<HTMLElement>(selector)?.focus(), {
      injector: this.injector,
    });
  }
  protected readonly statusLabel = orderStatusLabel;

  actionLabel(status: string): string {
    return ACTION_LABELS[status] ?? status;
  }

  async move(status: string, confirmed = false): Promise<void> {
    if (this.busy()) return;
    // Cancelling puts stock back and can't be undone, so it asks once.
    if (status === 'Cancelled' && !confirmed) {
      this.confirmCancel.set(true);
      // Move focus into the confirmation so keyboard and screen-reader users know it opened.
      this.focusAfterRender('#keep-order');
      return;
    }
    this.busy.set(true);
    try {
      const updated = await firstValueFrom(
        this.http.patch<AdminOrder>(
          `/api/admin/orders/${encodeURIComponent(this.number())}/status`,
          { status },
        ),
      );
      this.detail.set(updated);
      this.confirmCancel.set(false);
      this.toasts.show(`Order marked ${orderStatusLabel(status).toLowerCase()}.`);
    } catch (e) {
      this.toasts.show(toProblem(e).title, 'error');
      this.detail.reload();
    } finally {
      this.busy.set(false);
    }
  }
}
