import { ChangeDetectionStrategy, Component, computed, effect, input, signal } from '@angular/core';
import { AbstractControl } from '@angular/forms';
import { controlError } from '../../core/form-errors';

/**
 * Renders the first error of a control. The control's unified `events` stream (value, status,
 * touched) bumps a signal, so the component stays OnPush/zoneless friendly.
 */
@Component({
  selector: 'app-field-error',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (message(); as m) { <small class="field-error" role="alert">{{ m }}</small> }`,
})
export class FieldErrorComponent {
  readonly control = input.required<AbstractControl | null>();
  private readonly version = signal(0);
  protected readonly message = computed(() => {
    this.version();
    return controlError(this.control());
  });

  constructor() {
    effect((onCleanup) => {
      const control = this.control();
      if (!control) return;
      const sub = control.events.subscribe(() => this.version.update((v) => v + 1));
      onCleanup(() => sub.unsubscribe());
    });
  }
}
