import { Routes } from '@angular/router';
import { AdminLayout } from './admin-layout';

export const ADMIN_ROUTES: Routes = [
  {
    path: '',
    component: AdminLayout,
    children: [
      {
        path: '',
        title: 'Dashboard · Nokshi Kathar Math admin',
        loadComponent: () => import('./dashboard.page').then((m) => m.DashboardPage),
      },
      {
        path: 'orders',
        title: 'Orders · Nokshi Kathar Math admin',
        loadComponent: () => import('./orders.page').then((m) => m.AdminOrdersPage),
      },
      {
        path: 'orders/:number',
        title: 'Order · Nokshi Kathar Math admin',
        loadComponent: () => import('./order-detail.page').then((m) => m.AdminOrderDetailPage),
      },
      {
        path: 'products',
        title: 'Products · Nokshi Kathar Math admin',
        loadComponent: () => import('./products.page').then((m) => m.AdminProductsPage),
      },
      {
        path: 'products/:id',
        title: 'Edit product · Nokshi Kathar Math admin',
        loadComponent: () => import('./product-edit.page').then((m) => m.ProductEditPage),
      },
      {
        path: 'artisans',
        title: 'Artisans · Nokshi Kathar Math admin',
        loadComponent: () => import('./artisans.page').then((m) => m.AdminArtisansPage),
      },
    ],
  },
];
