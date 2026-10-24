import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ArtisanSummary, Category, Paged, ProductCard } from '../catalog/catalog.models';
import { ProductCardComponent } from '../catalog/product-card';

@Component({
  selector: 'app-home-page',
  imports: [RouterLink, ProductCardComponent],
  templateUrl: './home.page.html',
  styleUrl: './home.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomePage {
  protected readonly featured = httpResource<Paged<ProductCard>>(() => ({
    url: '/api/products',
    params: { type: 'unique', inStock: true, pageSize: 4 },
  }));
  protected readonly categories = httpResource<Category[]>(() => '/api/categories');
  protected readonly artisans = httpResource<ArtisanSummary[]>(() => '/api/artisans');

  // Generated kantha pattern (scripts/generate-placeholders.mjs) until the shop's own photography is ready.
  protected readonly heroImage = '/placeholders/hero.svg';
}
