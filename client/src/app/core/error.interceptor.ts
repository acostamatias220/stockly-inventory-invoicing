import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { ToastService } from './toast.service';
import { ProblemDetails } from './models';

/**
 * Global error feedback. Validation errors (400) are left to the forms, which map them to fields;
 * everything else is shown as a toast with the API's problem `detail`.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toast = inject(ToastService);
  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status !== 400 && error.status !== 401) {
        toast.error(problemMessage(error));
      }
      return throwError(() => error);
    }),
  );
};

export function problemMessage(error: HttpErrorResponse): string {
  if (error.status === 0) {
    return 'No se pudo conectar con el servidor.';
  }
  if (error.status === 429) {
    return 'Demasiados intentos. Esperá un minuto y probá de nuevo.';
  }
  const problem = error.error as ProblemDetails | null;
  return problem?.detail ?? problem?.title ?? 'Ocurrió un error inesperado.';
}
