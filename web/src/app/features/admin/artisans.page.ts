import { HttpClient, httpResource } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  afterNextRender,
  inject,
  signal,
} from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { toProblem } from '../../core/http/problem';
import { ToastService } from '../../shared/toast/toast.service';
import { AdminArtisan } from './admin.models';

const MESSAGES: Record<string, string> = {
  name: "Add the artisan's name.",
  region: 'Add where they work, e.g. Jashore.',
  story: 'Add a short story.',
};

@Component({
  selector: 'app-admin-artisans-page',
  imports: [ReactiveFormsModule],
  template: `
    <div class="page-head">
      <h1 tabindex="-1">Artisans</h1>
      <button
        class="btn"
        [class.btn--primary]="!editing()"
        [class.btn--ghost]="editing()"
        type="button"
        (click)="edit(null)"
      >
        New artisan
      </button>
    </div>

    <div class="layout">
      @if (artisans.hasValue()) {
        <ul class="rows">
          @for (a of artisans.value(); track a.id) {
            <li>
              <button
                class="row row--text row--button"
                type="button"
                (click)="edit(a)"
                [attr.aria-pressed]="editingId() === a.id"
              >
                <span class="row__main">
                  <strong>{{ a.name }}</strong>
                  <span class="row__meta">{{ a.region }}</span>
                </span>
                <span class="row__cell row__meta"
                  >{{ a.productCount }} {{ a.productCount === 1 ? 'piece' : 'pieces' }}</span
                >
              </button>
            </li>
          }
        </ul>
      } @else if (artisans.error()) {
        <p class="empty">
          Artisans didn't load.
          <button class="btn btn--ghost" type="button" (click)="artisans.reload()">
            Try again
          </button>
        </p>
      } @else {
        <p class="muted" aria-busy="true">Loading…</p>
      }

      @if (editing()) {
        <form
          class="panel form"
          [formGroup]="form"
          (ngSubmit)="save()"
          novalidate
          aria-labelledby="artisan-form-title"
        >
          <h2 id="artisan-form-title" tabindex="-1">
            {{ editingId() ? 'Edit artisan' : 'New artisan' }}
          </h2>
          @for (field of textFields; track field.name) {
            <div class="field">
              <label [for]="'a-' + field.name">{{ field.label }}</label>
              @if (field.name === 'story') {
                <textarea
                  [id]="'a-' + field.name"
                  rows="5"
                  [formControlName]="field.name"
                  [attr.aria-invalid]="!!errorFor(field.name)"
                  [attr.aria-describedby]="
                    errorFor(field.name) ? 'a-' + field.name + '-error' : null
                  "
                ></textarea>
              } @else {
                <input
                  [id]="'a-' + field.name"
                  [formControlName]="field.name"
                  [attr.aria-invalid]="!!errorFor(field.name)"
                  [attr.aria-describedby]="
                    errorFor(field.name) ? 'a-' + field.name + '-error' : null
                  "
                />
              }
              @if (errorFor(field.name); as m) {
                <p class="field__error" [id]="'a-' + field.name + '-error'">{{ m }}</p>
              }
            </div>
          }
          <div class="field">
            <label for="a-photo">Portrait</label>
            @if (form.controls.photoUrl.value; as photo) {
              <img class="portrait" [src]="photo" alt="" width="96" height="96" />
            }
            <input
              id="a-photo"
              type="file"
              accept="image/jpeg,image/png,image/webp"
              (change)="uploadPhoto($any($event.target))"
              aria-describedby="a-photo-hint"
            />
            <p class="field__hint" id="a-photo-hint">
              {{
                form.controls.photoUrl.value
                  ? 'A portrait is set. Choose a file to replace it.'
                  : 'Optional. JPEG, PNG or WebP up to 5 MB.'
              }}
            </p>
          </div>
          @if (error(); as message) {
            <p class="form-error" role="alert">{{ message }}</p>
          }
          <div class="form-actions">
            <button class="btn btn--primary" type="submit" [attr.aria-disabled]="saving()">
              Save
            </button>
            <button class="btn btn--ghost" type="button" (click)="close()">Close</button>
          </div>
        </form>
      }
    </div>
  `,
  styleUrl: './admin.scss',
  styles: `
    .layout {
      display: grid;
      gap: var(--space-5);
    }
    @media (min-width: 64rem) {
      .layout {
        grid-template-columns: 1fr 1fr;
        align-items: start;
      }
    }
    .row--button {
      width: 100%;
      border: 0;
      border-bottom: 1px solid var(--color-border);
      background: none;
      font: inherit;
      text-align: start;
      cursor: pointer;
    }
    .row--button[aria-pressed='true'] {
      background: var(--color-surface);
    }
    .form {
      display: grid;
      gap: var(--space-4);
    }
    .form h2:focus {
      outline: none;
    }
    .portrait {
      width: 4rem;
      height: 4rem;
      border-radius: 50%;
      object-fit: cover;
    }
    .form-actions {
      display: flex;
      gap: var(--space-3);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminArtisansPage {
  private readonly http = inject(HttpClient);
  private readonly toasts = inject(ToastService);
  private readonly injector = inject(Injector);

  protected readonly artisans = httpResource<AdminArtisan[]>(() => '/api/admin/artisans');
  protected readonly editing = signal(false);
  protected readonly editingId = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly textFields = [
    { name: 'name', label: 'Name' },
    { name: 'region', label: 'Region' },
    { name: 'story', label: 'Story' },
  ] as const;

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    region: ['', [Validators.required, Validators.maxLength(100)]],
    story: ['', [Validators.required, Validators.maxLength(4000)]],
    photoUrl: [''],
  });

  errorFor(name: 'name' | 'region' | 'story'): string | null {
    const c = this.form.controls[name];
    return c.touched && c.invalid ? MESSAGES[name] : null;
  }

  edit(artisan: AdminArtisan | null): void {
    this.editingId.set(artisan?.id ?? null);
    this.error.set(null);
    this.form.reset({
      name: artisan?.name ?? '',
      region: artisan?.region ?? '',
      story: artisan?.story ?? '',
      photoUrl: artisan?.photoUrl ?? '',
    });
    this.editing.set(true);
    // On phones the form opens below the list: move focus there so the change is announced.
    this.focusAfterRender('#artisan-form-title');
  }

  close(): void {
    const id = this.editingId();
    this.editing.set(false);
    this.focusAfterRender(id ? `[aria-pressed="false"]` : '.page-head .btn');
  }

  async uploadPhoto(input: HTMLInputElement): Promise<void> {
    const file = input.files?.[0];
    if (!file) return;
    try {
      const data = new FormData();
      data.append('file', file);
      const { url } = await firstValueFrom(
        this.http.post<{ url: string }>('/api/admin/uploads', data),
      );
      this.form.controls.photoUrl.setValue(url);
      this.toasts.show('Portrait uploaded. Save to keep it.');
    } catch (e) {
      this.error.set(toProblem(e).title);
      input.value = '';
    }
  }

  async save(): Promise<void> {
    if (this.saving()) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.focusAfterRender('.form [aria-invalid="true"]');
      return;
    }
    const body = {
      ...this.form.getRawValue(),
      photoUrl: this.form.controls.photoUrl.value || null,
    };
    const id = this.editingId();
    this.saving.set(true);
    try {
      await firstValueFrom(
        id
          ? this.http.put(`/api/admin/artisans/${id}`, body)
          : this.http.post('/api/admin/artisans', body),
      );
      this.toasts.show(`Saved ${body.name}.`);
      this.editing.set(false);
      this.artisans.reload();
      this.focusAfterRender('.page-head h1');
    } catch (e) {
      this.error.set(toProblem(e).title);
    } finally {
      this.saving.set(false);
    }
  }

  private focusAfterRender(selector: string): void {
    afterNextRender(() => document.querySelector<HTMLElement>(selector)?.focus(), {
      injector: this.injector,
    });
  }
}
