import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { ToastService } from '../../shared/toast/toast.service';

@Component({
  selector: 'app-site-header',
  imports: [RouterLink, RouterLinkActive, ReactiveFormsModule],
  templateUrl: './site-header.html',
  styleUrl: './site-header.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SiteHeader {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);

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

  logout(): void {
    this.auth.logout().subscribe(() => {
      this.toasts.show('You are signed out.');
      void this.router.navigateByUrl('/');
    });
  }
}
