import { Order } from '../checkout/checkout.models';

export interface AdminOrderListItem {
  number: string;
  status: string;
  createdAt: string;
  customerName: string;
  phone: string;
  itemCount: number;
  total: number;
}

export interface Dashboard {
  salesToday: number;
  salesThisWeek: number;
  ordersToday: number;
  pendingOrders: number;
  lowStock: { id: string; name: string; stockQuantity: number }[];
  recentOrders: AdminOrderListItem[];
}

export interface AdminProductListItem {
  id: string;
  name: string;
  slug: string;
  price: number;
  inventoryType: 'Unique' | 'Standard';
  stockQuantity: number;
  isActive: boolean;
  isReserved: boolean;
  imageUrl: string | null;
  categoryName: string;
}

export interface AdminImage {
  id: string;
  url: string;
  alt: string;
  sortOrder: number;
}

export interface AdminProduct {
  id: string;
  name: string;
  slug: string;
  description: string;
  materials: string;
  dimensions: string;
  price: number;
  inventoryType: 'Unique' | 'Standard';
  stockQuantity: number;
  isActive: boolean;
  categoryId: string;
  artisanId: string | null;
  version: number;
  images: AdminImage[];
}

export interface AdminOrder {
  order: Order;
  customerEmail: string;
  nextStatuses: string[];
}

export interface AdminArtisan {
  id: string;
  name: string;
  region: string;
  story: string;
  photoUrl: string | null;
  productCount: number;
}
