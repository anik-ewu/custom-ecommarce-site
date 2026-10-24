import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-home-page',
  template: `<p>The collection is on its way.</p>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomePage {}
