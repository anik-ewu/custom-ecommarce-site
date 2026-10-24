import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AccountPage } from './account.page';

describe('AccountPage', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [AccountPage],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
  });

  it('lists orders with a shopper-facing status and links to each order', async () => {
    const fixture = TestBed.createComponent(AccountPage);
    fixture.detectChanges();

    TestBed.inject(HttpTestingController)
      .expectOne((r) => r.url === '/api/orders')
      .flush({
        items: [
          {
            number: 'KB-20261024-ABCD',
            status: 'Pending',
            createdAt: '2026-10-24T04:00:00Z',
            total: 2720,
            itemCount: 3,
            firstItemName: 'Lotus Pond Cushion Cover',
            firstItemImageUrl: null,
          },
        ],
        page: 1,
        pageSize: 10,
        totalCount: 1,
        totalPages: 1,
      });
    await fixture.whenStable();
    fixture.detectChanges();

    const link = (fixture.nativeElement as HTMLElement).querySelector('a.order');
    expect(link?.getAttribute('href')).toBe('/orders/KB-20261024-ABCD');
    expect(link?.textContent).toContain('Awaiting confirmation');
    expect(link?.textContent).toContain('+ 2 more');
  });

  it('shows a next step when there are no orders', async () => {
    const fixture = TestBed.createComponent(AccountPage);
    fixture.detectChanges();

    TestBed.inject(HttpTestingController)
      .expectOne((r) => r.url === '/api/orders')
      .flush({ items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 });
    await fixture.whenStable();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('No orders yet');
    expect(el.querySelector('a[href="/shop"]')).not.toBeNull();
  });
});
