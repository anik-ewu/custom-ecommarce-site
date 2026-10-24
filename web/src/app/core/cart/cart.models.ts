export interface CartItem {
  id: string;
  productId: string;
  slug: string;
  name: string;
  imageUrl: string | null;
  unitPrice: number;
  currentPrice: number;
  quantity: number;
  isUnique: boolean;
  reservedUntil: string | null;
  available: boolean;
}

export interface Cart {
  items: CartItem[];
  subtotal: number;
  itemCount: number;
}
