import { Component, OnInit, signal, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormService } from '../../services/form.service';
import { FormSummary } from '../../models/form.model';

@Component({
  selector: 'app-my-forms',
  imports: [RouterLink],
  templateUrl: './my-forms.html',
  styleUrl: './my-forms.scss'
})
export class MyForms implements OnInit {
  forms   = signal<FormSummary[]>([]);
  loading = signal(true);
  error   = signal('');

  private readonly formService = inject(FormService);

  ngOnInit(): void {
    this.formService.listForms().subscribe({
      next: forms => { this.forms.set(forms); this.loading.set(false); },
      error: ()    => { this.error.set('Could not load forms.'); this.loading.set(false); }
    });
  }

  securityLabel(id: number): string {
    return id === 1 ? 'Public' : id === 2 ? 'Private' : 'URL Only';
  }

  securityClass(id: number): string {
    return id === 1 ? 'badge-public' : id === 2 ? 'badge-private' : 'badge-url';
  }
}
