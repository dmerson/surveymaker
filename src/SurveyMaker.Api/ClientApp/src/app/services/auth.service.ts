import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable } from 'rxjs';
import { tap } from 'rxjs/operators';
import { UserInfo } from '../models/user.model';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly userSubject = new BehaviorSubject<UserInfo>({ isAuthenticated: false });

  readonly currentUser$ = this.userSubject.asObservable();

  get currentUser(): UserInfo {
    return this.userSubject.value;
  }

  loadUser(): Observable<UserInfo> {
    return this.http.get<UserInfo>('/api/account/user').pipe(
      tap(user => this.userSubject.next(user))
    );
  }

  login(returnUrl: string = '/dashboard'): void {
    window.location.href = `/api/account/login?returnUrl=${encodeURIComponent(returnUrl)}`;
  }

  logout(): void {
    this.http.post<void>('/api/account/logout', {}).subscribe({
      next: () => {
        this.userSubject.next({ isAuthenticated: false });
        window.location.href = '/';
      },
      error: () => {
        window.location.href = '/';
      }
    });
  }
}
