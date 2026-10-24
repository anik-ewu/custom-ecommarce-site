import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ToastService } from './toast.service';

@Component({
  selector: 'app-toast-host',
  imports: [NgTemplateOutlet],
  template: `
    <div class="toasts">
      <!-- Errors interrupt (alert); confirmations wait for a pause (status). -->
      <div class="toasts__region" role="status" aria-live="polite">
        @for (toast of info(); track toast.id) {
          <ng-container *ngTemplateOutlet="item; context: { $implicit: toast }" />
        }
      </div>
      <div class="toasts__region" role="alert">
        @for (toast of errors(); track toast.id) {
          <ng-container *ngTemplateOutlet="item; context: { $implicit: toast }" />
        }
      </div>
    </div>

    <ng-template #item let-toast>
      <div class="toast" [class.toast--error]="toast.kind === 'error'">
        <span>{{ toast.message }}</span>
        <button
          type="button"
          class="toast__close"
          (click)="toasts.dismiss(toast.id)"
          aria-label="Dismiss"
        >
          ×
        </button>
      </div>
    </ng-template>
  `,
  styles: `
    .toasts {
      position: fixed;
      inset-inline: var(--space-4);
      bottom: var(--space-4);
      z-index: 50;
      pointer-events: none;
    }
    .toasts__region {
      display: grid;
      gap: var(--space-2);
      justify-items: center;
    }
    .toast {
      display: flex;
      align-items: center;
      gap: var(--space-3);
      max-width: 28rem;
      padding: var(--space-2) var(--space-2) var(--space-2) var(--space-4);
      border-radius: var(--radius-md);
      background: var(--color-ink);
      color: var(--color-bg);
      box-shadow: var(--shadow-md);
      pointer-events: auto;
      animation: toast-in var(--duration-slow) var(--ease);
    }
    .toast--error {
      background: var(--color-danger);
      color: var(--color-on-accent);
    }
    .toast__close {
      min-width: var(--tap-target);
      min-height: var(--tap-target);
      border: 0;
      background: none;
      color: inherit;
      font: inherit;
      font-size: var(--text-lg);
      cursor: pointer;
    }
    @keyframes toast-in {
      from {
        opacity: 0;
        transform: translateY(var(--space-2));
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ToastHost {
  protected readonly toasts = inject(ToastService);
  protected readonly info = computed(() => this.toasts.toasts().filter((t) => t.kind !== 'error'));
  protected readonly errors = computed(() =>
    this.toasts.toasts().filter((t) => t.kind === 'error'),
  );
}
