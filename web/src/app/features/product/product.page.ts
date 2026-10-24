import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { TakaPipe } from '../../shared/money/taka.pipe';
import { ProductDetail } from '../catalog/catalog.models';
import { ProductGallery } from './product-gallery';

@Component({
  selector: 'app-product-page',
  imports: [RouterLink, TakaPipe, ProductGallery],
  templateUrl: './product.page.html',
  styleUrl: './product.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductPage {
  readonly slug = input.required<string>();

  protected readonly product = httpResource<ProductDetail>(
    () => `/api/products/${encodeURIComponent(this.slug())}`,
  );
  protected readonly notFound = computed(
    () => (this.product.error() as HttpErrorResponse | undefined)?.status === 404,
  );

  constructor() {
    const title = inject(Title);
    effect(() => {
      // value() throws while the resource is in an error state, so check hasValue() first.
      if (this.product.hasValue()) title.setTitle(`${this.product.value().name} · Kinbo`);
    });
  }
}
