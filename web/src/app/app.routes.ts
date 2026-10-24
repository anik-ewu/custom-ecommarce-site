import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./features/home/home.page').then((m) => m.HomePage) },
  {
    path: 'shop',
    title: 'Shop · Kinbo',
    loadComponent: () => import('./features/catalog/catalog.page').then((m) => m.CatalogPage),
  },
  {
    path: 'product/:slug',
    title: 'Kinbo',
    loadComponent: () => import('./features/product/product.page').then((m) => m.ProductPage),
  },
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
