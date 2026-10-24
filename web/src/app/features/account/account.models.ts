export interface OrderSummary {
  number: string;
  status: string;
  createdAt: string;
  total: number;
  itemCount: number;
  firstItemName: string;
  firstItemImageUrl: string | null;
}
