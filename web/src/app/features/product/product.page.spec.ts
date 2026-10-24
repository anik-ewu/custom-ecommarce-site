import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ProductPage } from './product.page';

describe('ProductPage', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ProductPage],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
  });

  // Regression: reading value() on an errored resource threw, so a 404 stayed on the skeleton forever.
  it('shows the not-found state when the API returns 404', async () => {
    const fixture = TestBed.createComponent(ProductPage);
    fixture.componentRef.setInput('slug', 'nope');
    fixture.detectChanges();

    TestBed.inject(HttpTestingController)
      .expectOne('/api/products/nope')
      .flush({ code: 'NOT_FOUND' }, { status: 404, statusText: 'Not Found' });
    await fixture.whenStable();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('h1')?.textContent).toContain("We couldn't find that piece");
  });
});
