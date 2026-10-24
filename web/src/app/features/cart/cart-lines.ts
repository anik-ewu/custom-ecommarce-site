import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  inject,
  input,
  viewChildren,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { CartItem } from '../../core/cart/cart.models';
import { CartStore } from '../../core/cart/cart.store';
import { HoldCountdown } from '../../shared/countdown/hold-countdown';
import { TakaPipe } from '../../shared/money/taka.pipe';

@Component({
  selector: 'app-cart-lines',
  imports: [RouterLink, TakaPipe, HoldCountdown],
  templateUrl: './cart-lines.html',
  styleUrl: './cart-lines.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CartLines {
  protected readonly store = inject(CartStore);
  private readonly injector = inject(Injector);
  private readonly names = viewChildren<ElementRef<HTMLElement>>('name');

  readonly items = input.required<CartItem[]>();
  /** Where focus goes when the last line is removed (an element id with tabindex="-1"). */
  readonly emptyFocusId = input.required<string>();

  async remove(itemId: string, index: number): Promise<void> {
    if (!(await this.store.remove(itemId))) return;
    // The focused button is gone with its line: move to the next line, or to the heading when empty.
    afterNextRender(
      () => {
        const names = this.names();
        const target = names[Math.min(index, names.length - 1)]?.nativeElement;
        (target ?? document.getElementById(this.emptyFocusId()))?.focus();
      },
      { injector: this.injector },
    );
  }
}
