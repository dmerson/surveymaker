import { Component, OnInit, signal, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { SlicePipe } from '@angular/common';
import { FormService } from '../../services/form.service';
import { SubmissionSummary } from '../../models/form.model';

@Component({
  selector: 'app-form-answers',
  imports: [RouterLink, SlicePipe],
  templateUrl: './form-answers.html',
  styleUrl: './form-answers.scss'
})
export class FormAnswers implements OnInit {
  private readonly route       = inject(ActivatedRoute);
  private readonly formService = inject(FormService);

  formId      = signal('');
  formName    = signal('');
  submissions = signal<SubmissionSummary[]>([]);
  loading     = signal(true);
  error       = signal('');

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.formId.set(id);

    this.formService.listSubmissions(id).subscribe({
      next: res => {
        this.formName.set(res.formName);
        this.submissions.set(res.submissions);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load responses.');
        this.loading.set(false);
      }
    });
  }
}
