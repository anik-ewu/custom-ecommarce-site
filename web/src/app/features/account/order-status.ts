/** Shopper-facing wording for order statuses; the API sends the enum names. */
export const ORDER_STEPS = ['Pending', 'Confirmed', 'Shipped', 'Delivered'] as const;

const LABELS: Record<string, string> = {
  Pending: 'Awaiting confirmation',
  Confirmed: 'Confirmed',
  Shipped: 'On its way',
  Delivered: 'Delivered',
  Cancelled: 'Cancelled',
};

export const orderStatusLabel = (status: string) => LABELS[status] ?? status;
