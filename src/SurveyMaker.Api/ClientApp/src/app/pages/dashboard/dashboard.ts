import { Component, OnInit, computed, signal, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SlicePipe } from '@angular/common';
import { AuthService } from '../../services/auth.service';
import { FormService } from '../../services/form.service';
import { FormSummary } from '../../models/form.model';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, SlicePipe],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class Dashboard implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly formService = inject(FormService);

  readonly user = this.authService.currentUser;

  forms        = signal<FormSummary[]>([]);
  formsLoading = signal(true);

  recentForms = computed(() => this.forms().slice(0, 5));
  formCount   = computed(() => this.forms().length);

  get firstName(): string {
    const name = this.user.name ?? this.user.email ?? 'there';
    return name.split(' ')[0];
  }

  get greeting(): string {
    const hour = new Date().getHours();
    if (hour < 12) return 'Good morning';
    if (hour < 18) return 'Good afternoon';
    return 'Good evening';
  }

  ngOnInit(): void {
    this.formService.listForms().subscribe({
      next:  forms => { this.forms.set(forms); this.formsLoading.set(false); },
      error: ()    => { this.formsLoading.set(false); }
    });
  }

  securityLabel(id: number): string {
    return id === 1 ? 'Public' : id === 2 ? 'Private' : 'URL Only';
  }

  securityClass(id: number): string {
    return id === 1 ? 'badge-public' : id === 2 ? 'badge-private' : 'badge-url';
  }
}
