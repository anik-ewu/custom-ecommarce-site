export interface CheckoutSummary {
  subtotal: number;
  shippingFee: number;
  total: number;
  freeShippingThreshold: number;
  itemCount: number;
}

export interface ShippingAddress {
  fullName: string;
  phone: string;
  line1: string;
  line2: string | null;
  city: string;
  postalCode: string | null;
}

export interface Order {
  number: string;
  status: string;
  createdAt: string;
  paymentMethod: string;
  shippingAddress: ShippingAddress;
  items: { productId: string; name: string; price: number; quantity: number; lineTotal: number }[];
  subtotal: number;
  shippingFee: number;
  total: number;
}
