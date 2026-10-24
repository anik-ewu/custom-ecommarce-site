import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-admin-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="admin container">
      <nav class="admin__nav" aria-label="Admin">
        <p class="admin__title">Shop admin</p>
        <ul>
          <li>
            <a
              routerLink="/admin"
              routerLinkActive="is-active"
              [routerLinkActiveOptions]="{ exact: true }"
              ariaCurrentWhenActive="page"
              >Dashboard</a
            >
          </li>
          <li>
            <a routerLink="/admin/orders" routerLinkActive="is-active" ariaCurrentWhenActive="page"
              >Orders</a
            >
          </li>
          <li>
            <a
              routerLink="/admin/products"
              routerLinkActive="is-active"
              ariaCurrentWhenActive="page"
              >Products</a
            >
          </li>
          <li>
            <a
              routerLink="/admin/artisans"
              routerLinkActive="is-active"
              ariaCurrentWhenActive="page"
              >Artisans</a
            >
          </li>
        </ul>
      </nav>
      <div class="admin__main">
        <router-outlet />
      </div>
    </div>
  `,
  styles: `
    .admin {
      display: grid;
      gap: var(--space-5);
      padding-block: var(--space-5) var(--space-8);
    }
    .admin__title {
      display: none;
      margin: 0 0 var(--space-3);
      font-size: var(--text-xs);
      font-weight: var(--weight-semibold);
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: var(--color-muted);
    }
    .admin__nav {
      min-width: 0;
    }
    .admin__nav ul {
      display: flex;
      gap: var(--space-1);
      padding-block: var(--space-2);
      margin: 0 calc(-1 * var(--gutter));
      padding: 0 var(--gutter);
      overflow-x: auto;
      list-style: none;
      scrollbar-width: none;
    }
    .admin__nav a {
      display: inline-flex;
      align-items: center;
      min-height: var(--tap-target);
      padding-inline: var(--space-3);
      border-radius: var(--radius-pill);
      color: inherit;
      text-decoration: none;
      white-space: nowrap;
    }
    .admin__nav a.is-active {
      background: var(--color-ink);
      color: var(--color-bg);
    }
    .admin__main {
      min-width: 0;
    }
    @media (min-width: 56rem) {
      .admin {
        grid-template-columns: 12rem 1fr;
        gap: var(--space-8);
        align-items: start;
      }
      .admin__nav {
        position: sticky;
        top: calc(var(--header-height) + var(--space-5));
      }
      .admin__title {
        display: block;
      }
      .admin__nav ul {
        flex-direction: column;
        margin: 0;
        padding: var(--space-1);
        overflow: visible;
      }
      .admin__nav a {
        width: 100%;
        border-radius: var(--radius-md);
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminLayout {}
