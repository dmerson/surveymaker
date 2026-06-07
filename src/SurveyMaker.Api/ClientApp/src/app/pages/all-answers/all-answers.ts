import { Component, OnInit, signal, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { SlicePipe } from '@angular/common';
import { FormService } from '../../services/form.service';
import { AnswerGridQuestion, AnswerGridRow } from '../../models/form.model';

@Component({
  selector: 'app-all-answers',
  imports: [RouterLink, SlicePipe],
  templateUrl: './all-answers.html',
  styleUrl: './all-answers.scss'
})
export class AllAnswers implements OnInit {
  private readonly route       = inject(ActivatedRoute);
  private readonly formService = inject(FormService);

  formId    = signal('');
  formName  = signal('');
  questions = signal<AnswerGridQuestion[]>([]);
  rows      = signal<AnswerGridRow[]>([]);
  loading   = signal(true);
  error     = signal('');

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.formId.set(id);

    this.formService.getAnswerGrid(id).subscribe({
      next: grid => {
        this.formName.set(grid.formName);
        this.questions.set(grid.questions);
        this.rows.set(grid.rows);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load answers.');
        this.loading.set(false);
      }
    });
  }
}
