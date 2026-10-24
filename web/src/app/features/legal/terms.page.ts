import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SHOP_INFO } from '../../core/shop-info';

@Component({
  selector: 'app-terms-page',
  imports: [RouterLink],
  templateUrl: './terms.page.html',
  styleUrl: './legal.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TermsPage {
  protected readonly shop = SHOP_INFO;
}
