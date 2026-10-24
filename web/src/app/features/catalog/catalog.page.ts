import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Category, Paged, ProductCard, SortOption } from './catalog.models';
import { ProductCardComponent } from './product-card';

const PAGE_SIZE = 12;

@Component({
  selector: 'app-catalog-page',
  imports: [RouterLink, ProductCardComponent],
  templateUrl: './catalog.page.html',
  styleUrl: './catalog.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogPage {
  private readonly router = inject(Router);

  // Bound from the query string (withComponentInputBinding), so filters are shareable URLs.
  readonly category = input<string>();
  readonly q = input<string>();
  readonly sort = input<SortOption>();
  readonly type = input<'unique' | 'standard'>();
  readonly page = input<string>();

  protected readonly pageNumber = computed(() => Math.max(1, Number(this.page()) || 1));
  protected readonly skeletons = Array.from({ length: 8 }, (_, i) => i);

  protected readonly categories = httpResource<Category[]>(() => '/api/categories');
  protected readonly products = httpResource<Paged<ProductCard>>(() => ({
    url: '/api/products',
    params: {
      ...(this.category() ? { category: this.category()! } : {}),
      ...(this.q() ? { q: this.q()! } : {}),
      ...(this.type() ? { type: this.type()! } : {}),
      sort: this.sort() ?? 'newest',
      page: this.pageNumber(),
      pageSize: PAGE_SIZE,
    },
  }));

  protected readonly heading = computed(() => {
    const q = this.q();
    if (q) return `Results for “${q}”`;
    const slug = this.category();
    if (!slug) return this.type() === 'unique' ? 'One of a kind' : 'The collection';
    // value() throws on an errored resource; a neutral heading also avoids a flash while loading.
    if (!this.categories.hasValue()) return 'Shop';
    return this.categories.value().find((c) => c.slug === slug)?.name ?? 'The collection';
  });

  protected readonly hasFilters = computed(() => !!(this.category() || this.q() || this.type()));

  setSort(value: string): void {
    void this.router.navigate([], {
      queryParams: { sort: value === 'newest' ? null : value, page: null },
      queryParamsHandling: 'merge',
    });
  }
}
