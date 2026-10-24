import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TakaPipe } from '../../shared/money/taka.pipe';
import { Paged } from '../catalog/catalog.models';
import { AdminProductListItem } from './admin.models';

@Component({
  selector: 'app-admin-products-page',
  imports: [RouterLink, TakaPipe],
  template: `
    <div class="page-head">
      <h1>Products</h1>
      <a class="btn btn--primary" routerLink="/admin/products/new">New product</a>
    </div>

    <form
      class="toolbar"
      role="search"
      (submit)="$event.preventDefault(); apply(box.value, s.value)"
    >
      <label class="visually-hidden" for="product-search">Search products</label>
      <input
        #box
        id="product-search"
        type="search"
        [value]="q() ?? ''"
        placeholder="Name or web address"
      />
      <label class="visually-hidden" for="product-status">Filter by status</label>
      <select #s id="product-status" [value]="status() ?? ''" (change)="apply(box.value, s.value)">
        <option value="">All products</option>
        <option value="active">On sale</option>
        <option value="archived">Archived</option>
      </select>
      <button class="btn btn--ghost" type="submit">Search</button>
    </form>

    @if (products.hasValue()) {
      @let result = products.value();
      @if (result.items.length === 0) {
        <p class="empty">No products match.</p>
      } @else {
        <ul class="rows">
          @for (p of result.items; track p.id) {
            <li>
              <a class="row" [routerLink]="['/admin/products', p.id]">
                @if (p.imageUrl) {
                  <img [src]="p.imageUrl" alt="" width="48" height="60" loading="lazy" />
                } @else {
                  <span class="row__thumb" aria-hidden="true"></span>
                }
                <span class="row__main">
                  <strong>{{ p.name }}</strong>
                  <span class="row__meta">{{ p.categoryName }}</span>
                </span>
                <span class="row__cell">{{ p.price | taka }}</span>
                <span class="row__cell">
                  @if (p.inventoryType === 'Unique') {
                    {{
                      p.stockQuantity === 1
                        ? p.isReserved
                          ? 'One of a kind · held'
                          : 'One of a kind'
                        : 'Sold'
                    }}
                  } @else {
                    {{ p.stockQuantity }} in stock
                  }
                </span>
                <span class="row__cell">
                  <span class="pill" [class.pill--muted]="!p.isActive">{{
                    p.isActive ? 'On sale' : 'Archived'
                  }}</span>
                </span>
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
                >Previous</a
              >
            }
            <span class="muted">Page {{ pageNumber() }} of {{ result.totalPages }}</span>
            @if (pageNumber() < result.totalPages) {
              <a
                class="btn btn--ghost"
                [routerLink]="[]"
                [queryParams]="{ page: pageNumber() + 1 }"
                queryParamsHandling="merge"
                >Next</a
              >
            }
          </nav>
        }
      }
    } @else if (products.error()) {
      <p class="empty">
        Products didn't load.
        <button class="btn btn--ghost" type="button" (click)="products.reload()">Try again</button>
      </p>
    } @else {
      <p class="muted" aria-busy="true">Loading…</p>
    }
  `,
  styleUrl: './admin.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminProductsPage {
  private readonly router = inject(Router);

  readonly q = input<string>();
  readonly status = input<string>();
  readonly page = input<string>();

  protected readonly pageNumber = computed(() => Math.max(1, Number(this.page()) || 1));
  protected readonly products = httpResource<Paged<AdminProductListItem>>(() => ({
    url: '/api/admin/products',
    params: {
      ...(this.q() ? { q: this.q()! } : {}),
      ...(this.status() ? { status: this.status()! } : {}),
      page: this.pageNumber(),
    },
  }));

  apply(q: string, status: string): void {
    void this.router.navigate([], {
      queryParams: { q: q.trim() || null, status: status || null, page: null },
      queryParamsHandling: 'merge',
    });
  }
}
