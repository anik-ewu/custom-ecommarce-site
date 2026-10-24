import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TakaPipe } from '../../shared/money/taka.pipe';
import { orderStatusLabel } from '../account/order-status';
import { Paged } from '../catalog/catalog.models';
import { AdminOrderListItem } from './admin.models';

const STATUSES = ['Pending', 'Confirmed', 'Shipped', 'Delivered', 'Cancelled'];

@Component({
  selector: 'app-admin-orders-page',
  imports: [RouterLink, TakaPipe, DatePipe],
  template: `
    <div class="page-head"><h1>Orders</h1></div>

    <nav class="chips" aria-label="Filter by status">
      <a
        [routerLink]="[]"
        [queryParams]="{ status: null, page: null }"
        queryParamsHandling="merge"
        [attr.aria-current]="!status() ? 'page' : null"
        >All</a
      >
      @for (s of statuses; track s) {
        <a
          [routerLink]="[]"
          [queryParams]="{ status: s, page: null }"
          queryParamsHandling="merge"
          [attr.aria-current]="status() === s ? 'page' : null"
          >{{ statusLabel(s) }}</a
        >
      }
    </nav>

    <form class="toolbar" role="search" (submit)="$event.preventDefault(); search(box.value)">
      <label class="visually-hidden" for="order-search"
        >Search by order number, name or phone</label
      >
      <input
        #box
        id="order-search"
        type="search"
        [value]="q() ?? ''"
        placeholder="Order number, name or phone"
      />
      <button class="btn btn--ghost" type="submit">Search</button>
    </form>

    @if (orders.hasValue()) {
      @let result = orders.value();
      @if (result.items.length === 0) {
        <p class="empty">No orders match.</p>
      } @else {
        <ul class="rows">
          @for (o of result.items; track o.number) {
            <li>
              <a class="row row--text" [routerLink]="['/admin/orders', o.number]">
                <span class="row__main">
                  <strong>{{ o.number }}</strong>
                  <span class="row__meta">{{ o.customerName }} · {{ o.phone }}</span>
                </span>
                <span class="row__cell row__meta"
                  >{{ o.createdAt | date: 'd MMM y, h:mm a' }} · {{ o.itemCount }}
                  {{ o.itemCount === 1 ? 'item' : 'items' }}</span
                >
                <span class="row__cell"
                  ><span class="pill">{{ statusLabel(o.status) }}</span></span
                >
                <span class="row__cell">{{ o.total | taka }}</span>
              </a>
            </li>
          }
        </ul>
        @if (result.totalPages > 1) {
          <nav class="pager" aria-label="Pages">
            @if (pageNumber() > 1) {
              <a
                class="btn btn--ghost"
                [routerLink]="[]"
                [queryParams]="{ page: pageNumber() - 1 }"
                queryParamsHandling="merge"
                >Newer</a
              >
            }
            <span class="muted">Page {{ pageNumber() }} of {{ result.totalPages }}</span>
            @if (pageNumber() < result.totalPages) {
              <a
                class="btn btn--ghost"
                [routerLink]="[]"
                [queryParams]="{ page: pageNumber() + 1 }"
                queryParamsHandling="merge"
                >Older</a
              >
            }
          </nav>
        }
      }
    } @else if (orders.error()) {
      <p class="empty">
        Orders didn't load.
        <button class="btn btn--ghost" type="button" (click)="orders.reload()">Try again</button>
      </p>
    } @else {
      <p class="muted" aria-busy="true">Loading…</p>
    }
  `,
  styleUrl: './admin.scss',
  styles: `
    .chips {
      display: flex;
      gap: var(--space-2);
      margin: 0 calc(-1 * var(--gutter)) var(--space-4);
      padding: var(--space-2) var(--gutter);
      overflow-x: auto;
      scrollbar-width: none;
    }
    .chips a {
      flex: none;
      display: inline-flex;
      align-items: center;
      min-height: var(--tap-target);
      padding-inline: var(--space-4);
      border: 1px solid var(--color-border-strong);
      border-radius: var(--radius-pill);
      color: inherit;
      font-size: var(--text-sm);
      text-decoration: none;
      white-space: nowrap;
    }
    .chips a[aria-current='page'] {
      background: var(--color-ink);
      border-color: var(--color-ink);
      color: var(--color-bg);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminOrdersPage {
  private readonly router = inject(Router);

  readonly status = input<string>();
  readonly q = input<string>();
  readonly page = input<string>();

  protected readonly statuses = STATUSES;
  protected readonly statusLabel = orderStatusLabel;
  protected readonly pageNumber = computed(() => Math.max(1, Number(this.page()) || 1));
  protected readonly orders = httpResource<Paged<AdminOrderListItem>>(() => ({
    url: '/api/admin/orders',
    params: {
      ...(this.status() ? { status: this.status()! } : {}),
      ...(this.q() ? { q: this.q()! } : {}),
      page: this.pageNumber(),
    },
  }));

  search(q: string): void {
    void this.router.navigate([], {
      queryParams: { q: q.trim() || null, page: null },
      queryParamsHandling: 'merge',
    });
  }
}
