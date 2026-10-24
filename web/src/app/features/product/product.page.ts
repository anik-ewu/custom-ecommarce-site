import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { CartStore } from '../../core/cart/cart.store';
import { HoldCountdown } from '../../shared/countdown/hold-countdown';
import { TakaPipe } from '../../shared/money/taka.pipe';
import { ProductDetail } from '../catalog/catalog.models';
import { ProductGallery } from './product-gallery';

@Component({
  selector: 'app-product-page',
  imports: [RouterLink, TakaPipe, ProductGallery, HoldCountdown],
  templateUrl: './product.page.html',
  styleUrl: './product.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductPage {
  readonly slug = input.required<string>();
  protected readonly cart = inject(CartStore);

  protected readonly product = httpResource<ProductDetail>(
    () => `/api/products/${encodeURIComponent(this.slug())}`,
  );
  protected readonly notFound = computed(
    () => (this.product.error() as HttpErrorResponse | undefined)?.status === 404,
  );

  async addToCart(productId: string): Promise<void> {
    if (this.cart.busy()) return;
    await this.cart.add(productId);
    // Refresh availability either way: it changed if we got the hold, and may have if someone else did.
    this.product.reload();
  }

  constructor() {
    const title = inject(Title);
    effect(() => {
      // value() throws while the resource is in an error state, so check hasValue() first.
      if (this.product.hasValue())
        title.setTitle(`${this.product.value().name} · Nokshi Kathar Math`);
    });
  }
}
