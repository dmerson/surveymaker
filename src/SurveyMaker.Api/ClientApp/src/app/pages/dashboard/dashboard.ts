import { Component, OnInit, signal, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SlicePipe } from '@angular/common';
import { AuthService } from '../../services/auth.service';
import { FormService } from '../../services/form.service';
import { DashboardData, DashboardFormSummary, DashboardActivity } from '../../models/form.model';

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

  fromDate = signal(this.defaultFrom());
  toDate   = signal(this.today());
  loading  = signal(true);
  error    = signal('');

  myFormsCount      = signal(0);
  responsesReceived = signal(0);
  surveysCompleted  = signal(0);
  recentForms       = signal<DashboardFormSummary[]>([]);
  recentActivity    = signal<DashboardActivity[]>([]);

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
    this.load();
  }

  onDateChange(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.formService.getDashboard(this.fromDate(), this.toDate()).subscribe({
      next: (data: DashboardData) => {
        this.myFormsCount.set(data.myFormsCount);
        this.responsesReceived.set(data.responsesReceived);
        this.surveysCompleted.set(data.surveysCompleted);
        this.recentForms.set(data.recentForms);
        this.recentActivity.set(data.recentActivity);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load dashboard data.');
        this.loading.set(false);
      }
    });
  }

  securityLabel(id: number): string {
    return id === 1 ? 'Public' : id === 2 ? 'Private' : 'URL Only';
  }

  securityClass(id: number): string {
    return id === 1 ? 'badge-public' : id === 2 ? 'badge-private' : 'badge-url';
  }

  private today(): string {
    return new Date().toISOString().slice(0, 10);
  }

  private defaultFrom(): string {
    const d = new Date();
    d.setDate(d.getDate() - 29);
    return d.toISOString().slice(0, 10);
  }
}
