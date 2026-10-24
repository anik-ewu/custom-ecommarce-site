import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';

@Component({
  selector: 'app-product-gallery',
  template: `
    @let image = current();
    @if (image) {
      <div
        class="stage"
        [class.stage--zoom]="zoomed()"
        (pointerenter)="zoomed.set(true)"
        (pointerleave)="zoomed.set(false)"
        (pointermove)="move($event)"
      >
        <img
          [src]="image.url"
          [alt]="image.alt"
          width="900"
          height="1125"
          fetchpriority="high"
          [style.transform-origin]="origin()"
        />
      </div>
    }
    @if (images().length > 1) {
      <ul class="thumbs" aria-label="More photos">
        @for (img of images(); track img.url; let i = $index) {
          <li>
            <button
              type="button"
              class="thumb"
              [attr.aria-pressed]="i === index()"
              [attr.aria-label]="'Show photo ' + (i + 1) + ': ' + img.alt"
              (click)="index.set(i)"
            >
              <img [src]="img.url" alt="" width="90" height="112" loading="lazy" />
            </button>
          </li>
        }
      </ul>
    }
  `,
  styles: `
    :host {
      display: grid;
      gap: var(--space-3);
    }
    /* Square on phones so the name and price stay near the first screen; portrait from tablets up. */
    .stage {
      overflow: hidden;
      aspect-ratio: 1;
      border-radius: var(--radius-lg);
      background: var(--color-soft);
    }
    @media (min-width: 56rem) {
      .stage {
        aspect-ratio: 4 / 5;
      }
    }
    .stage img {
      width: 100%;
      height: 100%;
      object-fit: cover;
      transition: transform var(--duration-slow) var(--ease);
    }
    /* Zoom only where a pointer can hover; touch users see the full photo. */
    @media (hover: hover) {
      .stage--zoom {
        cursor: zoom-in;
      }
      .stage--zoom img {
        transform: scale(1.8);
      }
    }
    .thumbs {
      display: flex;
      gap: var(--space-2);
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .thumb {
      width: 4.5rem;
      padding: 0;
      overflow: hidden;
      border: 2px solid transparent;
      border-radius: var(--radius-md);
      background: none;
      cursor: pointer;
    }
    .thumb[aria-pressed='true'] {
      border-color: var(--color-accent-strong);
    }
    .thumb img {
      aspect-ratio: 4 / 5;
      object-fit: cover;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductGallery {
  readonly images = input.required<{ url: string; alt: string }[]>();

  protected readonly index = signal(0);
  protected readonly zoomed = signal(false);
  protected readonly origin = signal('50% 50%');
  protected readonly current = computed(() => this.images()[this.index()] ?? this.images()[0]);

  move(event: PointerEvent): void {
    const box = (event.currentTarget as HTMLElement).getBoundingClientRect();
    const x = ((event.clientX - box.left) / box.width) * 100;
    const y = ((event.clientY - box.top) / box.height) * 100;
    this.origin.set(`${x}% ${y}%`);
  }
}
