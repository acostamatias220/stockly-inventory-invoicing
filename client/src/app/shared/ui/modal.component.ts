import { ChangeDetectionStrategy, Component, ElementRef, effect, input, output, viewChild } from '@angular/core';

/** Accessible modal built on the native <dialog> element (focus trap and Esc handled by the browser). */
@Component({
  selector: 'app-modal',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dialog #dialog class="modal" [attr.aria-labelledby]="titleId" (close)="closed.emit()" (cancel)="closed.emit()">
      <header class="modal-header">
        <h2 [id]="titleId">{{ title() }}</h2>
        <button type="button" class="icon-btn" aria-label="Cerrar" (click)="closed.emit()">✕</button>
      </header>
      <div class="modal-body"><ng-content /></div>
    </dialog>
  `,
})
export class ModalComponent {
  private static nextId = 0;
  readonly title = input.required<string>();
  readonly open = input(false);
  readonly closed = output<void>();
  protected readonly titleId = `modal-title-${ModalComponent.nextId++}`;
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  constructor() {
    effect(() => {
      const el = this.dialog().nativeElement;
      if (this.open() && !el.open) {
        el.showModal?.();
      } else if (!this.open() && el.open) {
        el.close();
      }
    });
  }
}
