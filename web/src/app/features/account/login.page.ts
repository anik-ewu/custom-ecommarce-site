import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { User } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { toProblem } from '../../core/http/problem';

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './login.page.html',
  styleUrl: './auth-form.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly returnUrl = input<string>('/');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { email, password } = this.form.getRawValue();
    this.run(this.auth.login(email, password));
  }

  tryDemo(role: 'customer' | 'admin'): void {
    this.run(this.auth.demo(role));
  }

  private run(request: Observable<User>): void {
    this.busy.set(true);
    this.error.set(null);
    request.subscribe({
      next: () => void this.router.navigateByUrl(this.returnUrl() || '/'),
      error: (e: unknown) => {
        this.error.set(toProblem(e).title);
        this.busy.set(false);
      },
    });
  }
}
