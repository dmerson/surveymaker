import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map, take } from 'rxjs/operators';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.currentUser.isAuthenticated) {
    return true;
  }

  return auth.loadUser().pipe(
    take(1),
    map(user => user.isAuthenticated
      ? true
      : router.createUrlTree(['/'], { queryParams: { returnUrl: state.url } })
    )
  );
};
