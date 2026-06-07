import { Component, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { FormService } from '../../services/form.service';

@Component({
  selector: 'app-create-form',
  imports: [FormsModule, RouterLink],
  templateUrl: './create-form.html',
  styleUrl: './create-form.scss'
})
export class CreateForm {
  title          = signal('');
  description    = signal('');
  securityTypeId = signal(1);
  saving         = signal(false);
  error          = signal('');

  private readonly formService = inject(FormService);
  private readonly router      = inject(Router);

  create(): void {
    if (!this.title().trim()) {
      this.error.set('Form title is required.');
      return;
    }
    this.saving.set(true);
    this.error.set('');

    this.formService
      .create(this.title().trim(), this.description().trim() || undefined, this.securityTypeId())
      .subscribe({
        next: ({ formId }) => this.router.navigate(['/forms', formId, 'edit']),
        error: () => {
          this.error.set('Failed to create form. Please try again.');
          this.saving.set(false);
        }
      });
  }
}
