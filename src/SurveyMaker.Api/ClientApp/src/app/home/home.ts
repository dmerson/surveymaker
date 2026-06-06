import { Component, OnInit, inject } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { AuthService } from '../services/auth.service';

@Component({
  selector: 'app-home',
  imports: [],
  templateUrl: './home.html',
  styleUrl: './home.scss'
})
export class Home implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  loading = true;

  ngOnInit(): void {
    this.authService.loadUser().subscribe({
      next: user => {
        if (user.isAuthenticated) {
          const returnUrl = this.route.snapshot.queryParams['returnUrl'] ?? '/dashboard';
          this.router.navigateByUrl(returnUrl);
        } else {
          this.loading = false;
        }
      },
      error: () => { this.loading = false; }
    });
  }

  signIn(): void {
    const returnUrl = this.route.snapshot.queryParams['returnUrl'] ?? '/dashboard';
    this.authService.login(returnUrl);
  }
}
