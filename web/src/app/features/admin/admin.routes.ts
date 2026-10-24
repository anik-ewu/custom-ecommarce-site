import { Routes } from '@angular/router';
import { AdminLayout } from './admin-layout';

export const ADMIN_ROUTES: Routes = [
  {
    path: '',
    component: AdminLayout,
    children: [
      {
        path: '',
        title: 'Dashboard · Kinbo admin',
        loadComponent: () => import('./dashboard.page').then((m) => m.DashboardPage),
      },
      {
        path: 'orders',
        title: 'Orders · Kinbo admin',
        loadComponent: () => import('./orders.page').then((m) => m.AdminOrdersPage),
      },
      {
        path: 'orders/:number',
        title: 'Order · Kinbo admin',
        loadComponent: () => import('./order-detail.page').then((m) => m.AdminOrderDetailPage),
      },
      {
        path: 'products',
        title: 'Products · Kinbo admin',
        loadComponent: () => import('./products.page').then((m) => m.AdminProductsPage),
      },
      {
        path: 'products/:id',
        title: 'Edit product · Kinbo admin',
        loadComponent: () => import('./product-edit.page').then((m) => m.ProductEditPage),
      },
      {
        path: 'artisans',
        title: 'Artisans · Kinbo admin',
        loadComponent: () => import('./artisans.page').then((m) => m.AdminArtisansPage),
      },
    ],
  },
];
