import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';

/** "Held for you · 14:32" for a reserved one-of-a-kind piece. Emits `expired` once when the hold runs out. */
@Component({
  selector: 'app-hold-countdown',
  template: `
    @if (remaining() > 0) {
      <span class="hold">
        Held for you ·
        <span role="timer" aria-live="off" [attr.aria-label]="label()">{{ clock() }}</span>
      </span>
    } @else {
      <span class="hold hold--ended">Your hold has ended</span>
    }
  `,
  styles: `
    .hold {
      font-size: var(--text-sm);
      color: var(--color-accent-strong);
      font-variant-numeric: tabular-nums;
    }
    .hold--ended {
      color: var(--color-muted);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HoldCountdown {
  readonly until = input.required<string>();
  readonly expired = output<void>();

  private readonly now = signal(Date.now());
  protected readonly remaining = computed(() => Math.max(0, Date.parse(this.until()) - this.now()));
  protected readonly clock = computed(() => {
    const total = Math.ceil(this.remaining() / 1000);
    return `${Math.floor(total / 60)}:${String(total % 60).padStart(2, '0')}`;
  });
  protected readonly label = computed(
    () => `${Math.ceil(this.remaining() / 60000)} minutes left on your hold`,
  );

  constructor() {
    const id = setInterval(() => this.now.set(Date.now()), 1000);
    inject(DestroyRef).onDestroy(() => clearInterval(id));

    let fired = false;
    effect(() => {
      if (this.remaining() === 0 && !fired) {
        fired = true;
        this.expired.emit();
      }
    });
  }
}
