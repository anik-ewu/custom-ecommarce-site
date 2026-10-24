import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { TakaPipe } from '../../shared/money/taka.pipe';
import { ToastService } from '../../shared/toast/toast.service';
import { Paged } from '../catalog/catalog.models';
import { OrderSummary } from './account.models';
import { orderStatusLabel } from './order-status';

const PAGE_SIZE = 10;

@Component({
  selector: 'app-account-page',
  imports: [RouterLink, TakaPipe, DatePipe],
  templateUrl: './account.page.html',
  styleUrl: './account.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountPage {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);

  protected readonly page = signal(1);
  protected readonly orders = httpResource<Paged<OrderSummary>>(() => ({
    url: '/api/orders',
    params: { page: this.page(), pageSize: PAGE_SIZE },
  }));
  protected readonly statusLabel = orderStatusLabel;

  signOut(): void {
    this.auth.logout().subscribe(() => {
      this.toasts.show('You are signed out.');
      void this.router.navigateByUrl('/');
    });
  }
}
