import { HttpClient, HttpHeaders, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export type ApiRequestOptions = {
  headers?: HttpHeaders;
  withCredentials?: boolean;
};

@Injectable({ providedIn: 'root' })
export class ApiClientService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1';

  get<T>(path: string, options: ApiRequestOptions = {}): Observable<T> {
    return this.http.get<T>(`${this.baseUrl}${path}`, { ...options, withCredentials: true });
  }

  getResponse<T>(path: string, options: ApiRequestOptions = {}): Observable<HttpResponse<T>> {
    return this.http.get<T>(`${this.baseUrl}${path}`, {
      ...options,
      withCredentials: true,
      observe: 'response',
    });
  }

  post<T>(path: string, body: unknown, options: ApiRequestOptions = {}): Observable<T> {
    return this.http.post<T>(`${this.baseUrl}${path}`, body, { ...options, withCredentials: true });
  }

  put<T>(path: string, body: unknown, options: ApiRequestOptions = {}): Observable<T> {
    return this.http.put<T>(`${this.baseUrl}${path}`, body, { ...options, withCredentials: true });
  }

  postResponse<T>(path: string, body: unknown, options: ApiRequestOptions = {}): Observable<HttpResponse<T>> {
    return this.http.post<T>(`${this.baseUrl}${path}`, body, {
      ...options,
      withCredentials: true,
      observe: 'response',
    });
  }
}
