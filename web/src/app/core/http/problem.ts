import { HttpErrorResponse } from '@angular/common/http';

/** The API's error shape: ProblemDetails with a stable `code` (see PROJECT_PROMPT.md §7). */
export interface Problem {
  status: number;
  title: string;
  code: string;
  errors?: Record<string, string[]>;
}

export function toProblem(error: unknown): Problem {
  if (error instanceof HttpErrorResponse && error.error && typeof error.error === 'object') {
    const body = error.error as Partial<Problem>;
    return {
      status: error.status,
      title: body.title ?? 'Something went wrong. Please try again.',
      code: body.code ?? 'INTERNAL_ERROR',
      errors: body.errors,
    };
  }
  return { status: 0, title: 'Could not reach the shop. Check your connection.', code: 'NETWORK' };
}
