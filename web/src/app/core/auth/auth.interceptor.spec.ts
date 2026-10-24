import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { authInterceptor } from './auth.interceptor';
import { AuthResponse } from './auth.models';

const refreshed: AuthResponse = {
  accessToken: 'new-token',
  expiresAt: new Date().toISOString(),
  user: { id: '1', email: 'a@b.c', name: 'A', role: 'Customer' },
};

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('shares one refresh between concurrent 401s and retries both with the new token', () => {
    const results: string[] = [];
    http.get<string>('/api/orders').subscribe((r) => results.push(r));
    http.get<string>('/api/cart').subscribe((r) => results.push(r));

    backend.expectOne('/api/orders').flush(null, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne('/api/cart').flush(null, { status: 401, statusText: 'Unauthorized' });

    backend.expectOne('/api/auth/refresh').flush(refreshed);

    const retries = backend.match((r) => r.url === '/api/orders' || r.url === '/api/cart');
    expect(retries.length).toBe(2);
    for (const retry of retries) {
      expect(retry.request.headers.get('Authorization')).toBe('Bearer new-token');
      retry.flush(retry.request.url);
    }
    expect(results.sort()).toEqual(['/api/cart', '/api/orders']);
  });

  it('does not retry auth endpoints on 401', () => {
    let failed = false;
    http.post('/api/auth/login', {}).subscribe({ error: () => (failed = true) });
    backend.expectOne('/api/auth/login').flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(failed).toBe(true);
  });
});
