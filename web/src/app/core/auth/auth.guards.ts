import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  return auth.isLoggedIn()
    ? true
    : inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

/** The API enforces the Admin role too; this only keeps customers out of the admin screens. */
export const adminGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  if (auth.isAdmin()) return true;
  return auth.isLoggedIn()
    ? inject(Router).createUrlTree(['/'])
    : inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};
