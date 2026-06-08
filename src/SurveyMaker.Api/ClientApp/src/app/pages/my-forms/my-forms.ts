import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SlicePipe } from '@angular/common';
import { FormService } from '../../services/form.service';
import { FormSummary } from '../../models/form.model';

@Component({
  selector: 'app-my-forms',
  imports: [RouterLink, SlicePipe],
  templateUrl: './my-forms.html',
  styleUrl: './my-forms.scss'
})
export class MyForms implements OnInit {
  forms        = signal<FormSummary[]>([]);
  loading      = signal(true);
  error        = signal('');
  copiedId     = signal<string | null>(null);

  filterFrom   = signal('');
  filterTo     = signal('');
  filterStatus = signal<'all' | 'draft' | 'published'>('all');

  filteredForms = computed(() => {
    const from   = this.filterFrom();
    const to     = this.filterTo();
    const status = this.filterStatus();
    return this.forms().filter(f => {
      const date = f.createdAt.slice(0, 10);
      if (from && date < from) return false;
      if (to   && date > to)   return false;
      if (status === 'draft'     &&  f.published) return false;
      if (status === 'published' && !f.published) return false;
      return true;
    });
  });

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

  clearFilters(): void {
    this.filterFrom.set('');
    this.filterTo.set('');
    this.filterStatus.set('all');
  }

  copyLink(formId: string): void {
    const url = `${window.location.origin}/survey/${formId}`;
    navigator.clipboard.writeText(url).then(() => {
      this.copiedId.set(formId);
      setTimeout(() => this.copiedId.set(null), 2000);
    });
  }
}
