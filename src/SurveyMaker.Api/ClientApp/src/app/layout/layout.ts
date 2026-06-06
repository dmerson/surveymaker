import { Component, inject } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../services/auth.service';

@Component({
  selector: 'app-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './layout.html',
  styleUrl: './layout.scss'
})
export class Layout {
  private readonly authService = inject(AuthService);

  get user() { return this.authService.currentUser; }

  get initials(): string {
    const name = this.user.name ?? this.user.email ?? '';
    return name.split(' ').map(w => w[0]).join('').substring(0, 2).toUpperCase() || '?';
  }

  signOut(): void {
    this.authService.logout();
  }
}
