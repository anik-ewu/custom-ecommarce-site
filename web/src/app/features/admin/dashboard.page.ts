import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TakaPipe } from '../../shared/money/taka.pipe';
import { orderStatusLabel } from '../account/order-status';
import { Dashboard } from './admin.models';

@Component({
  selector: 'app-dashboard-page',
  imports: [RouterLink, TakaPipe, DatePipe],
  template: `
    <div class="page-head"><h1>Dashboard</h1></div>

    @if (dashboard.hasValue()) {
      @let d = dashboard.value();
      <dl class="cards">
        <div class="card">
          <dt>Sales today</dt>
          <dd>{{ d.salesToday | taka }}</dd>
        </div>
        <div class="card">
          <dt>Last 7 days</dt>
          <dd>{{ d.salesThisWeek | taka }}</dd>
        </div>
        <div class="card">
          <dt>Orders today</dt>
          <dd>{{ d.ordersToday }}</dd>
        </div>
        <div class="card card--accent">
          <dt>Awaiting confirmation</dt>
          <dd>
            <a
              routerLink="/admin/orders"
              [queryParams]="{ status: 'Pending' }"
              [attr.aria-label]="d.pendingOrders + ' orders awaiting confirmation'"
              >{{ d.pendingOrders }}</a
            >
          </dd>
        </div>
      </dl>

      <div class="grid">
        <section class="panel" aria-labelledby="recent-title">
          <h2 id="recent-title">Recent orders</h2>
          @if (d.recentOrders.length === 0) {
            <p class="empty">No orders yet.</p>
          } @else {
            <ul class="rows">
              @for (o of d.recentOrders; track o.number) {
                <li>
                  <a class="row row--text" [routerLink]="['/admin/orders', o.number]">
                    <span class="row__main">
                      <strong>{{ o.number }}</strong>
                      <span class="row__meta"
                        >{{ o.customerName }} · {{ o.createdAt | date: 'd MMM, h:mm a' }}</span
                      >
                    </span>
                    <span class="row__cell"
                      ><span class="pill">{{ statusLabel(o.status) }}</span></span
                    >
                    <span class="row__cell">{{ o.total | taka }}</span>
                  </a>
                </li>
              }
            </ul>
          }
        </section>

        <section class="panel" aria-labelledby="stock-title">
          <h2 id="stock-title">Low stock</h2>
          @if (d.lowStock.length === 0) {
            <p class="empty">Every jute item has more than 3 in stock.</p>
          } @else {
            <ul class="stock">
              @for (p of d.lowStock; track p.id) {
                <li>
                  <a [routerLink]="['/admin/products', p.id]">{{ p.name }}</a>
                  <span class="pill" [class.pill--warn]="p.stockQuantity === 0"
                    >{{ p.stockQuantity }} left</span
                  >
                </li>
              }
            </ul>
          }
        </section>
      </div>
    } @else if (dashboard.error()) {
      <p class="empty">
        The dashboard didn't load.
        <button class="btn btn--ghost" type="button" (click)="dashboard.reload()">Try again</button>
      </p>
    } @else {
      <p class="muted" aria-busy="true">Loading…</p>
    }
  `,
  styleUrl: './admin.scss',
  styles: `
    .cards {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: var(--space-3);
      margin: 0 0 var(--space-5);
    }
    @media (min-width: 64rem) {
      .cards {
        grid-template-columns: repeat(4, minmax(0, 1fr));
      }
    }
    .card {
      display: grid;
      gap: var(--space-1);
      padding: var(--space-4);
      border-radius: var(--radius-lg);
      background: var(--color-surface);
    }
    .card dt {
      font-size: var(--text-sm);
      color: var(--color-muted);
    }
    .card dd {
      margin: 0;
      font-family: var(--font-display);
      font-size: var(--text-2xl);
    }
    .card--accent {
      border: var(--stitch-weight) dashed var(--color-stitch);
    }
    .card a {
      display: inline-flex;
      min-width: var(--tap-target);
      min-height: var(--tap-target);
      align-items: center;
      color: inherit;
    }
    .grid {
      display: grid;
      gap: var(--space-5);
    }
    @media (min-width: 64rem) {
      .grid {
        grid-template-columns: 2fr 1fr;
        align-items: start;
      }
    }
    .stock {
      display: grid;
      gap: 0;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .stock li {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--space-3);
      border-bottom: 1px solid var(--color-border);
    }
    .stock a {
      display: inline-flex;
      align-items: center;
      min-height: var(--tap-target);
      color: inherit;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardPage {
  protected readonly dashboard = httpResource<Dashboard>(() => '/api/admin/dashboard');
  protected readonly statusLabel = orderStatusLabel;
}
