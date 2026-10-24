import { HttpClient, httpResource } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Title } from '@angular/platform-browser';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { toProblem } from '../../core/http/problem';
import { ToastService } from '../../shared/toast/toast.service';
import { ArtisanSummary, Category } from '../catalog/catalog.models';
import { AdminProduct } from './admin.models';

const MESSAGES: Record<string, string> = {
  name: 'Give the product a name.',
  slug: 'Use lowercase letters, numbers and dashes.',
  description: 'Add a short description.',
  materials: 'List the materials.',
  dimensions: 'Add the dimensions.',
  price: 'Enter a price of at least Tk 1.',
  stockQuantity: 'Enter a stock of 0 or more.',
  categoryId: 'Choose a category.',
};

@Component({
  selector: 'app-product-edit-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './product-edit.page.html',
  styleUrls: ['./admin.scss', './product-edit.page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductEditPage {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);
  private readonly injector = inject(Injector);

  /** Route param: a product id, or "new". */
  readonly id = input.required<string>();
  protected readonly isNew = computed(() => this.id() === 'new');

  protected readonly categories = httpResource<Category[]>(() => '/api/categories');
  protected readonly artisans = httpResource<ArtisanSummary[]>(() => '/api/artisans');
  protected readonly product = httpResource<AdminProduct>(() =>
    this.isNew() ? undefined : `/api/admin/products/${encodeURIComponent(this.id())}`,
  );

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly serverErrors = signal<Record<string, string[]>>({});
  protected readonly uploading = signal(false);
  protected readonly photoError = signal<string | null>(null);
  protected readonly pendingRemoveId = signal<string | null>(null);
  private version: number | null = null;

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    slug: ['', [Validators.maxLength(200), Validators.pattern(/^[a-z0-9]+(-[a-z0-9]+)*$/)]],
    description: ['', [Validators.required, Validators.maxLength(4000)]],
    materials: ['', [Validators.required, Validators.maxLength(500)]],
    dimensions: ['', [Validators.required, Validators.maxLength(500)]],
    price: [0, [Validators.required, Validators.min(1)]],
    inventoryType: ['Standard' as 'Unique' | 'Standard'],
    stockQuantity: [1, [Validators.required, Validators.min(0)]],
    categoryId: ['', Validators.required],
    artisanId: [''],
    isActive: [true],
  });

  protected readonly imageForm = inject(NonNullableFormBuilder).group({
    alt: ['', [Validators.required, Validators.maxLength(300)]],
  });

  constructor() {
    const title = inject(Title);
    effect(() => {
      if (this.product.hasValue()) this.load(this.product.value());
    });
    // The component is reused when navigating from an edit page to /new: start from a blank form then.
    effect(() => {
      title.setTitle(`${this.isNew() ? 'New product' : 'Edit product'} · Nokshi Kathar Math admin`);
      if (this.isNew()) {
        this.version = null;
        this.form.reset();
        this.error.set(null);
        this.serverErrors.set({});
      }
    });
  }

  /** The message under a field, or null: client validation first, then the server's error for that field. */
  errorFor(name: keyof typeof this.form.controls): string | null {
    const c = this.form.controls[name];
    if (c.touched && c.invalid) {
      if (c.errors?.['unique']) return 'A one-of-a-kind piece has a stock of 0 or 1.';
      return MESSAGES[name] ?? 'Check this field.';
    }
    const key = Object.keys(this.serverErrors()).find(
      (k) => k.split('.').pop()?.toLowerCase() === name.toLowerCase(),
    );
    return key ? (this.serverErrors()[key]?.[0] ?? null) : null;
  }

  /** aria-describedby: the field's hint (if any) plus its error message while it has one. */
  describedBy(name: keyof typeof this.form.controls, hintId?: string): string | null {
    const ids = [hintId, this.errorFor(name) ? `p-${name}-error` : undefined].filter(Boolean);
    return ids.length ? ids.join(' ') : null;
  }

  async save(): Promise<void> {
    if (this.saving()) return;
    const v = this.form.getRawValue();
    if (v.inventoryType === 'Unique' && v.stockQuantity > 1) {
      this.form.controls.stockQuantity.setErrors({ unique: true });
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.focusAfterRender('[aria-invalid="true"]');
      return;
    }

    const body = {
      ...v,
      slug: v.slug || null,
      artisanId: v.artisanId || null,
      version: this.version,
    };
    this.saving.set(true);
    this.error.set(null);
    this.serverErrors.set({});
    try {
      const saved = await firstValueFrom(
        this.isNew()
          ? this.http.post<AdminProduct>('/api/admin/products', body)
          : this.http.put<AdminProduct>(`/api/admin/products/${this.id()}`, body),
      );
      this.toasts.show(`Saved ${saved.name}.`);
      if (this.isNew()) {
        void this.router.navigate(['/admin/products', saved.id], { replaceUrl: true });
      } else {
        this.load(saved);
      }
    } catch (e) {
      const problem = toProblem(e);
      this.error.set(problem.title);
      this.serverErrors.set(problem.errors ?? {});
      this.focusAfterRender('#product-error');
    } finally {
      this.saving.set(false);
    }
  }

  async archive(): Promise<void> {
    if (this.saving()) return;
    this.saving.set(true);
    try {
      await firstValueFrom(this.http.delete(`/api/admin/products/${this.id()}`));
      this.toasts.show('Archived. It no longer shows in the shop.');
      this.product.reload();
    } catch (e) {
      this.toasts.show(toProblem(e).title, 'error');
    } finally {
      this.saving.set(false);
    }
  }

  async upload(input: HTMLInputElement): Promise<void> {
    if (this.uploading()) return;
    const file = input.files?.[0];
    this.photoError.set(null);
    if (this.imageForm.invalid) {
      this.imageForm.markAllAsTouched();
      document.getElementById('image-alt')?.focus();
      return;
    }
    if (!file) {
      this.photoError.set('Choose a photo to upload.');
      input.focus();
      return;
    }

    this.uploading.set(true);
    try {
      const data = new FormData();
      data.append('file', file);
      const { url } = await firstValueFrom(
        this.http.post<{ url: string }>('/api/admin/uploads', data),
      );
      const updated = await firstValueFrom(
        this.http.post<AdminProduct>(`/api/admin/products/${this.id()}/images`, {
          url,
          alt: this.imageForm.controls.alt.value,
        }),
      );
      this.product.set(updated);
      this.imageForm.reset();
      input.value = '';
      this.toasts.show('Photo added.');
    } catch (e) {
      this.photoError.set(toProblem(e).title);
    } finally {
      this.uploading.set(false);
    }
  }

  async removeImage(imageId: string): Promise<void> {
    this.pendingRemoveId.set(null);
    try {
      this.product.set(
        await firstValueFrom(
          this.http.delete<AdminProduct>(`/api/admin/products/${this.id()}/images/${imageId}`),
        ),
      );
      this.toasts.show('Photo removed.');
      this.focusAfterRender('#photos-title');
    } catch (e) {
      this.toasts.show(toProblem(e).title, 'error');
    }
  }

  private focusAfterRender(selector: string): void {
    afterNextRender(() => document.querySelector<HTMLElement>(selector)?.focus(), {
      injector: this.injector,
    });
  }

  private load(p: AdminProduct): void {
    this.version = p.version;
    this.form.reset({
      name: p.name,
      slug: p.slug,
      description: p.description,
      materials: p.materials,
      dimensions: p.dimensions,
      price: p.price,
      inventoryType: p.inventoryType,
      stockQuantity: p.stockQuantity,
      categoryId: p.categoryId,
      artisanId: p.artisanId ?? '',
      isActive: p.isActive,
    });
  }
}
