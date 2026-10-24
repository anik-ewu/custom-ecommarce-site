import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, finalize, map, of, shareReplay, tap } from 'rxjs';
import { AuthResponse, User } from './auth.models';

/**
 * Holds the access token in memory only (never localStorage). The refresh token lives in an
 * httpOnly cookie the browser sends to /api/auth, so a page reload restores the session via refresh().
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly token = signal<string | null>(null);
  private refreshing$: Observable<boolean> | null = null;

  readonly user = signal<User | null>(null);
  readonly isLoggedIn = computed(() => this.user() !== null);
  readonly isAdmin = computed(() => this.user()?.role === 'Admin');

  accessToken(): string | null {
    return this.token();
  }

  login(email: string, password: string): Observable<User> {
    return this.http
      .post<AuthResponse>('/api/auth/login', { email, password })
      .pipe(map((r) => this.accept(r)));
  }

  register(name: string, email: string, password: string): Observable<User> {
    return this.http
      .post<AuthResponse>('/api/auth/register', { name, email, password })
      .pipe(map((r) => this.accept(r)));
  }

  demo(role: 'customer' | 'admin'): Observable<User> {
    return this.http
      .post<AuthResponse>('/api/auth/demo', { role })
      .pipe(map((r) => this.accept(r)));
  }

  /** Single-flight: concurrent callers share one /refresh request. Emits true when a session exists. */
  refresh(): Observable<boolean> {
    this.refreshing$ ??= this.http.post<AuthResponse>('/api/auth/refresh', {}).pipe(
      tap((r) => this.accept(r)),
      map(() => true),
      catchError(() => {
        this.clear();
        return of(false);
      }),
      finalize(() => (this.refreshing$ = null)),
      shareReplay(1),
    );
    return this.refreshing$;
  }

  logout(): Observable<void> {
    return this.http.post<void>('/api/auth/logout', {}).pipe(
      catchError(() => of(undefined)),
      finalize(() => this.clear()),
    );
  }

  private accept(response: AuthResponse): User {
    this.token.set(response.accessToken);
    this.user.set(response.user);
    return response.user;
  }

  private clear(): void {
    this.token.set(null);
    this.user.set(null);
  }
}
