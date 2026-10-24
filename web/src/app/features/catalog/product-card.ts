import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CartStore } from '../../core/cart/cart.store';
import { TakaPipe } from '../../shared/money/taka.pipe';
import { ProductCard } from './catalog.models';

@Component({
  selector: 'app-product-card',
  imports: [RouterLink, TakaPipe],
  template: `
    @let p = product();
    <a class="card" [routerLink]="['/product', p.slug]">
      <div class="card__media">
        @if (p.imageUrl) {
          <img
            [src]="p.imageUrl"
            alt=""
            width="900"
            height="1125"
            loading="lazy"
            decoding="async"
          />
        }
        @if (p.isUnique) {
          <span class="badge">{{ badge() }}</span>
        }
        @if (!p.inStock) {
          <span class="badge badge--muted">{{ p.isUnique ? 'Sold' : 'Out of stock' }}</span>
        }
      </div>
      <div class="card__body">
        <h3 class="card__name">{{ p.name }}</h3>
        @if (p.artisanName) {
          <p class="card__artisan muted">by {{ p.artisanName }}</p>
        }
        <p class="card__price">{{ p.price | taka }}</p>
      </div>
    </a>
  `,
  styleUrl: './product-card.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductCardComponent {
  private readonly cart = inject(CartStore);
  readonly product = input.required<ProductCard>();

  protected readonly badge = computed(() => {
    const p = this.product();
    if (this.cart.holds(p.id)) return 'In your cart';
    return p.isReserved && p.inStock ? 'Reserved' : 'One of a kind';
  });
}
