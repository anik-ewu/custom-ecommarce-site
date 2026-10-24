export interface Category {
  id: string;
  name: string;
  slug: string;
  imageUrl: string | null;
}

export interface ProductCard {
  id: string;
  slug: string;
  name: string;
  price: number;
  isUnique: boolean;
  inStock: boolean;
  isReserved: boolean;
  imageUrl: string | null;
  imageAlt: string | null;
  categorySlug: string;
  artisanName: string | null;
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ArtisanSummary {
  id: string;
  name: string;
  region: string;
  story: string;
  photoUrl: string | null;
}

export interface ProductDetail {
  id: string;
  slug: string;
  name: string;
  description: string;
  materials: string;
  dimensions: string;
  price: number;
  isUnique: boolean;
  availableQuantity: number;
  reservedUntil: string | null;
  images: { url: string; alt: string }[];
  category: Category;
  artisan: ArtisanSummary | null;
}

export type SortOption = 'newest' | 'price-asc' | 'price-desc';

export interface ArtisanDetail {
  artisan: ArtisanSummary;
  products: ProductCard[];
}
