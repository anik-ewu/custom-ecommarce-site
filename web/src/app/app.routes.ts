import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./features/home/home.page').then((m) => m.HomePage) },
  {
    path: 'login',
    title: 'Sign in · Kinbo',
    loadComponent: () => import('./features/account/login.page').then((m) => m.LoginPage),
  },
  {
    path: 'register',
    title: 'Create account · Kinbo',
    loadComponent: () => import('./features/account/register.page').then((m) => m.RegisterPage),
  },
  { path: '**', redirectTo: '' },
];
