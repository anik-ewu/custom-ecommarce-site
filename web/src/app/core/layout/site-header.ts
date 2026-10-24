import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { CartStore } from '../cart/cart.store';

@Component({
  selector: 'app-site-header',
  imports: [RouterLink, RouterLinkActive, ReactiveFormsModule],
  templateUrl: './site-header.html',
  styleUrl: './site-header.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SiteHeader {
  protected readonly auth = inject(AuthService);
  protected readonly cart = inject(CartStore);
  private readonly router = inject(Router);

  protected readonly searchForm = inject(NonNullableFormBuilder).group({ q: '' });

  constructor() {
    // Keep the box in step with ?q, so the search you ran stays visible on the results page.
    this.router.events
      .pipe(
        filter((e) => e instanceof NavigationEnd),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe(() => {
        const q = this.router.parseUrl(this.router.url).queryParams['q'] ?? '';
        this.searchForm.controls.q.setValue(q);
      });
  }

  search(): void {
    const q = this.searchForm.controls.q.value.trim();
    void this.router.navigate(['/shop'], { queryParams: q ? { q } : {} });
  }
}
