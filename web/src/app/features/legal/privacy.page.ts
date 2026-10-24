import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SHOP_INFO } from '../../core/shop-info';

@Component({
  selector: 'app-privacy-page',
  imports: [RouterLink],
  templateUrl: './privacy.page.html',
  styleUrl: './legal.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrivacyPage {
  protected readonly shop = SHOP_INFO;
}
