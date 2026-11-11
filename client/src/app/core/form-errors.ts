import { HttpErrorResponse } from '@angular/common/http';
import { AbstractControl, FormGroup } from '@angular/forms';
import { ProblemDetails } from './models';

/**
 * Maps a 400 ValidationProblemDetails (`{ errors: { "lines[0].quantity": [...] } }`) onto the reactive
 * form controls so server-side rules show next to the right field. Returns unmapped messages.
 */
export function applyServerErrors(form: FormGroup, error: unknown): string[] {
  if (!(error instanceof HttpErrorResponse) || error.status !== 400) {
    return [];
  }
  const problem = error.error as ProblemDetails;
  const unmapped: string[] = [];
  for (const [path, messages] of Object.entries(problem.errors ?? {})) {
    const control = form.get(path.replace(/\[(\d+)\]/g, '.$1'));
    if (control) {
      control.setErrors({ server: messages[0] });
      control.markAsTouched();
    } else {
      unmapped.push(...messages);
    }
  }
  return unmapped;
}

/** First human readable error of a control (client or server side). */
export function controlError(control: AbstractControl | null): string | null {
  if (!control || !control.errors || !(control.touched || control.dirty)) {
    return null;
  }
  const e = control.errors;
  if (e['server']) return e['server'] as string;
  if (e['required']) return 'Campo obligatorio.';
  if (e['email']) return 'Email inválido.';
  if (e['min']) return `Debe ser mayor o igual a ${(e['min'] as { min: number }).min}.`;
  if (e['max']) return `Debe ser menor o igual a ${(e['max'] as { max: number }).max}.`;
  if (e['minlength']) return `Mínimo ${(e['minlength'] as { requiredLength: number }).requiredLength} caracteres.`;
  if (e['maxlength']) return `Máximo ${(e['maxlength'] as { requiredLength: number }).requiredLength} caracteres.`;
  if (e['pattern']) return 'Formato inválido.';
  if (e['cuit']) return 'CUIT inválido (revisá el dígito verificador).';
  return 'Valor inválido.';
}

/** Client-side CUIT/CUIL check digit validation (same algorithm as the API). */
export function isValidCuit(value: string): boolean {
  if (!/^\d{2}-?\d{8}-?\d$/.test(value)) return false;
  const digits = value.replace(/\D/g, '');
  const weights = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];
  const sum = weights.reduce((acc, w, i) => acc + w * Number(digits[i]), 0);
  let check = 11 - (sum % 11);
  if (check === 11) check = 0;
  if (check === 10) check = 9;
  return check === Number(digits[10]);
}
