import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { toProblem } from '../../core/http/problem';
import { ToastService } from '../../shared/toast/toast.service';

@Component({
  selector: 'app-register-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './register.page.html',
  styleUrl: './auth-form.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);

  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly fieldErrors = signal<Record<string, string[]>>({});

  readonly form = inject(NonNullableFormBuilder).group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  // Re-evaluate field messages whenever the form's value or touched state changes.
  private readonly formEvents = toSignal(this.form.events);

  nameError(): string | null {
    return this.clientOrServerError('name', 'Tell us your name.');
  }

  emailError(): string | null {
    return this.clientOrServerError('email', 'Enter a valid email address.');
  }

  passwordError(): string | null {
    return this.clientOrServerError('password', 'Use at least 8 characters.');
  }

  private clientOrServerError(
    field: 'name' | 'email' | 'password',
    message: string,
  ): string | null {
    this.formEvents();
    const control = this.form.controls[field];
    if (control.touched && control.invalid) return message;
    return this.fieldErrors()[field]?.[0] ?? null;
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { name, email, password } = this.form.getRawValue();
    this.busy.set(true);
    this.error.set(null);
    this.auth.register(name, email, password).subscribe({
      next: () => {
        this.toasts.show('Your account is ready.');
        void this.router.navigateByUrl('/');
      },
      error: (e: unknown) => {
        const problem = toProblem(e);
        this.error.set(problem.title);
        this.fieldErrors.set(problem.errors ?? {});
        this.busy.set(false);
      },
    });
  }
}
