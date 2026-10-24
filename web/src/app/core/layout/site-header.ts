import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
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

  protected readonly query = new FormControl('', { nonNullable: true });

  search(): void {
    const q = this.query.value.trim();
    void this.router.navigate(['/shop'], { queryParams: q ? { q } : {} });
  }

  logout(): void {
    this.auth.logout().subscribe(() => {
      this.toasts.show('You are signed out.');
      void this.router.navigateByUrl('/');
    });
  }
}
