import { HttpInterceptorFn } from '@angular/common/http';

function correlationId(): string {
  return globalThis.crypto?.randomUUID?.() ?? `ui-${Date.now().toString(36)}`;
}

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const withCookie = request.clone({ withCredentials: true });
  return next(
    withCookie.headers.has('X-Correlation-ID')
      ? withCookie
      : withCookie.clone({ setHeaders: { 'X-Correlation-ID': correlationId() } }),
  );
};
