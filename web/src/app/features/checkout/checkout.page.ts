import { HttpClient, httpResource } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  afterNextRender,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { CartStore } from '../../core/cart/cart.store';
import { toProblem } from '../../core/http/problem';
import { TakaPipe } from '../../shared/money/taka.pipe';
import { ToastService } from '../../shared/toast/toast.service';
import { CheckoutSummary, Order } from './checkout.models';

const BD_MOBILE = /^(\+?880|0)1[3-9]\d{8}$/;

/** Shoppers often type 01712-345678 or 01712 345678; accept that and send the digits only. */
const normalisePhone = (value: string) => value.replace(/[\s-]/g, '');

function bdMobile(control: AbstractControl<string>): ValidationErrors | null {
  return !control.value || BD_MOBILE.test(normalisePhone(control.value))
    ? null
    : { bdMobile: true };
}

@Component({
  selector: 'app-checkout-page',
  imports: [ReactiveFormsModule, RouterLink, TakaPipe],
  templateUrl: './checkout.page.html',
  styleUrl: './checkout.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CheckoutPage {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);
  private readonly injector = inject(Injector);
  protected readonly cart = inject(CartStore);

  protected readonly summary = httpResource<CheckoutSummary>(() => '/api/checkout/summary');
  protected readonly placing = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly serverErrors = signal<Record<string, string[]>>({});

  protected readonly form = inject(NonNullableFormBuilder).group({
    fullName: [
      inject(AuthService).user()?.name ?? '',
      [Validators.required, Validators.maxLength(100)],
    ],
    phone: ['', [Validators.required, bdMobile]],
    line1: ['', [Validators.required, Validators.maxLength(200)]],
    line2: ['', Validators.maxLength(200)],
    city: ['', [Validators.required, Validators.maxLength(100)]],
    postalCode: ['', Validators.maxLength(20)],
    paymentMethod: ['CashOnDelivery'],
  });

  constructor() {
    void this.cart.load();
    // A server field error is about the old value; clear it once the shopper edits.
    this.form.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => {
      if (Object.keys(this.serverErrors()).length) this.serverErrors.set({});
    });
  }

  /** Focus after the next render, once error messages and replaced controls are in the DOM. */
  private focusAfterRender(selector: string): void {
    afterNextRender(() => document.querySelector<HTMLElement>(selector)?.focus(), {
      injector: this.injector,
    });
  }

  invalid(name: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[name];
    // Server keys look like "ShippingAddress.Phone"; match on the last segment, ignoring case.
    const serverError = Object.keys(this.serverErrors()).some(
      (key) => key.split('.').pop()?.toLowerCase() === name.toLowerCase(),
    );
    return (control.touched && control.invalid) || serverError;
  }

  async holdAgain(productId: string): Promise<void> {
    if (await this.cart.add(productId)) {
      // add() opens the drawer for browsing; here the shopper is already checking out.
      this.cart.drawerOpen.set(false);
      this.error.set(null);
      this.summary.reload();
      this.focusAfterRender('#summary-title');
    }
  }

  async place(): Promise<void> {
    if (this.placing()) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.focusAfterRender('[aria-invalid="true"]');
      return;
    }

    const { paymentMethod, line2, postalCode, ...address } = this.form.getRawValue();
    this.placing.set(true);
    this.error.set(null);
    this.serverErrors.set({});
    try {
      const order = await firstValueFrom(
        this.http.post<Order>('/api/checkout', {
          paymentMethod,
          shippingAddress: {
            ...address,
            phone: normalisePhone(address.phone),
            line2: line2 || null,
            postalCode: postalCode || null,
          },
        }),
      );
      await this.cart.load();
      this.toasts.show(`Order ${order.number} placed. Thank you!`);
      void this.router.navigate(['/orders', order.number]);
    } catch (e) {
      const problem = toProblem(e);
      this.error.set(problem.title);
      this.serverErrors.set(problem.errors ?? {});
      // Price, stock or hold changes: show the cart as it is now, so the shopper can review and try again.
      if (['PRICE_CHANGED', 'OUT_OF_STOCK', 'ITEM_RESERVED'].includes(problem.code)) {
        this.summary.reload();
        await this.cart.load();
      }
      this.focusAfterRender('#checkout-error');
    } finally {
      this.placing.set(false);
    }
  }
}
