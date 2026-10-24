import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SHOP_INFO } from '../../core/shop-info';
import { PrivacyPage } from './privacy.page';
import { TermsPage } from './terms.page';

describe('Legal pages', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideRouter([])] }));

  // Google's OAuth verification looks for these on the privacy policy.
  it('privacy policy covers Google user data and gives a contact', async () => {
    const fixture = TestBed.createComponent(PrivacyPage);
    await fixture.whenStable();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Google API Services User Data Policy');
    expect(text).toContain('Limited Use');
    expect(text).toContain(SHOP_INFO.contactEmail);
  });

  it('terms explain holds, cash on delivery and returns', async () => {
    const fixture = TestBed.createComponent(TermsPage);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('#holds')).not.toBeNull();
    expect(el.querySelector('#payment')?.nextElementSibling?.textContent).toContain(
      'cash on delivery',
    );
    expect(el.querySelector('#returns')).not.toBeNull();
  });
});
