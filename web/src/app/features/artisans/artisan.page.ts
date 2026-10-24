import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { ArtisanDetail } from '../catalog/catalog.models';
import { ProductCardComponent } from '../catalog/product-card';

@Component({
  selector: 'app-artisan-page',
  imports: [RouterLink, ProductCardComponent],
  template: `
    <div class="container page">
      @if (detail.hasValue()) {
        @let d = detail.value();
        <article class="artisan" aria-labelledby="artisan-title">
          @if (d.artisan.photoUrl) {
            <img [src]="d.artisan.photoUrl" alt="" width="600" height="600" />
          }
          <div>
            <p class="eyebrow muted">{{ d.artisan.region }}</p>
            <h1 id="artisan-title">{{ d.artisan.name }}</h1>
            <p class="story">{{ d.artisan.story }}</p>
          </div>
        </article>

        <hr class="stitch-divider" />

        <section aria-labelledby="pieces-title">
          <h2 id="pieces-title">Pieces by {{ d.artisan.name.split(' ')[0] }}</h2>
          @if (d.products.length === 0) {
            <p class="muted">
              New pieces are on the frame. <a routerLink="/shop">See the collection</a>
            </p>
          } @else {
            <ul class="grid">
              @for (p of d.products; track p.id) {
                <li><app-product-card [product]="p" /></li>
              }
            </ul>
          }
        </section>
      } @else if (notFound()) {
        <div class="missing">
          <h1>We couldn't find that artisan</h1>
          <a class="btn btn--primary" routerLink="/shop">Explore the collection</a>
        </div>
      } @else if (detail.error()) {
        <div class="missing">
          <h1>Something went wrong</h1>
          <button class="btn btn--ghost" type="button" (click)="detail.reload()">Try again</button>
        </div>
      } @else {
        <div class="skeleton" aria-busy="true" aria-label="Loading artisan"></div>
      }
    </div>
  `,
  styles: `
    .page {
      padding-block: var(--space-6) var(--space-8);
    }
    .artisan {
      display: grid;
      gap: var(--space-6);
      align-items: center;
    }
    .artisan img {
      width: min(100%, 18rem);
      aspect-ratio: 1;
      border-radius: 50%;
      object-fit: cover;
    }
    @media (min-width: 48rem) {
      .artisan {
        grid-template-columns: 18rem 1fr;
        gap: var(--space-8);
      }
    }
    .eyebrow {
      margin: 0 0 var(--space-2);
      font-size: var(--text-xs);
      letter-spacing: 0.08em;
      text-transform: uppercase;
    }
    .story {
      max-width: 38rem;
      font-family: var(--font-display);
      font-size: var(--text-xl);
      line-height: var(--leading-body);
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: var(--space-6) var(--space-4);
      margin: var(--space-5) 0 0;
      padding: 0;
      list-style: none;
    }
    @media (min-width: 56rem) {
      .grid {
        grid-template-columns: repeat(4, minmax(0, 1fr));
      }
    }
    .missing {
      display: grid;
      justify-items: start;
      gap: var(--space-3);
      padding-block: var(--space-9);
    }
    .skeleton {
      height: 18rem;
      border-radius: var(--radius-lg);
      background: var(--color-soft);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ArtisanPage {
  readonly id = input.required<string>();

  protected readonly detail = httpResource<ArtisanDetail>(
    () => `/api/artisans/${encodeURIComponent(this.id())}`,
  );
  protected readonly notFound = computed(() => {
    const status = (this.detail.error() as HttpErrorResponse | undefined)?.status;
    // A malformed id doesn't match the :guid route constraint, which also ends in a 404.
    return status === 404;
  });

  constructor() {
    const title = inject(Title);
    effect(() => {
      if (this.detail.hasValue()) title.setTitle(`${this.detail.value().artisan.name} · Kinbo`);
    });
  }
}
