import { Routes } from '@angular/router';
import { adminGuard, authGuard } from './core/auth/auth.guards';

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
    path: 'checkout',
    title: 'Checkout · Kinbo',
    canActivate: [authGuard],
    loadComponent: () => import('./features/checkout/checkout.page').then((m) => m.CheckoutPage),
  },
  {
    path: 'orders/:number',
    title: 'Your order · Kinbo',
    canActivate: [authGuard],
    loadComponent: () => import('./features/account/order.page').then((m) => m.OrderPage),
  },
  {
    path: 'account',
    title: 'Your account · Kinbo',
    canActivate: [authGuard],
    loadComponent: () => import('./features/account/account.page').then((m) => m.AccountPage),
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
  {
    path: 'admin',
    canActivate: [adminGuard],
    loadChildren: () => import('./features/admin/admin.routes').then((m) => m.ADMIN_ROUTES),
  },
  {
    path: 'privacy',
    title: 'Privacy policy · Kinbo',
    loadComponent: () => import('./features/legal/privacy.page').then((m) => m.PrivacyPage),
  },
  {
    path: 'terms',
    title: 'Terms and conditions · Kinbo',
    loadComponent: () => import('./features/legal/terms.page').then((m) => m.TermsPage),
  },
  { path: '**', redirectTo: '' },
];
