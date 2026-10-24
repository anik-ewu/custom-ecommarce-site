import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-site-footer',
  imports: [RouterLink],
  template: `
    <footer class="footer">
      <div class="container footer__inner">
        <div>
          <p class="footer__name">Kinbo</p>
          <p class="muted">Nakshi kantha and jute, made by hand in Bangladesh.</p>
        </div>
        <nav aria-label="Footer">
          <ul>
            <li><a class="stitch-link" routerLink="/shop">Shop the collection</a></li>
            <li><a class="stitch-link" routerLink="/login">Your account</a></li>
          </ul>
        </nav>
        <p class="muted footer__small">Cash on delivery across Bangladesh · © {{ year }} Kinbo</p>
      </div>
    </footer>
  `,
  styles: `
    .footer {
      margin-top: var(--space-10);
      padding-block: var(--space-8);
      border-top: 1px solid var(--color-border);
      background: var(--color-surface);
    }
    .footer__inner {
      display: grid;
      gap: var(--space-6);
    }
    .footer__name {
      margin: 0 0 var(--space-2);
      font-family: var(--font-display);
      font-size: var(--text-xl);
    }
    ul a {
      display: inline-flex;
      align-items: center;
      min-height: var(--tap-target);
    }
    ul {
      display: grid;
      gap: 0;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .footer__small {
      font-size: var(--text-sm);
      margin: 0;
    }
    @media (min-width: 48rem) {
      .footer__inner {
        grid-template-columns: 2fr 1fr;
      }
      .footer__small {
        grid-column: 1 / -1;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SiteFooter {
  protected readonly year = new Date().getFullYear();
}
