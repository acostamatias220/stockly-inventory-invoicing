import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ToastService } from '../../core/toast.service';

@Component({
  selector: 'app-toast-container',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="toasts" aria-live="polite">
      @for (toast of toasts.toasts(); track toast.id) {
        <div class="toast" [class]="'toast toast-' + toast.kind" role="status">
          <span>{{ toast.message }}</span>
          <button type="button" class="icon-btn" aria-label="Cerrar aviso" (click)="toasts.dismiss(toast.id)">✕</button>
        </div>
      }
    </div>
  `,
})
export class ToastContainerComponent {
  protected readonly toasts = inject(ToastService);
}
