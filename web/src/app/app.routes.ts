import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    title: 'Kinbo · Handmade nakshi kantha and jute',
    loadComponent: () => import('./features/home/home.page').then((m) => m.HomePage),
  },
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
    path: 'artisans/:id',
    title: 'Artisan · Kinbo',
    loadComponent: () => import('./features/artisans/artisan.page').then((m) => m.ArtisanPage),
  },
  {
    path: 'cart',
    title: 'Your cart · Kinbo',
    loadComponent: () => import('./features/cart/cart.page').then((m) => m.CartPage),
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
