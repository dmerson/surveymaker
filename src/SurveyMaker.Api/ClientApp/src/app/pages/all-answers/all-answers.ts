import { Component, OnInit, computed, signal, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { SlicePipe } from '@angular/common';
import { FormService } from '../../services/form.service';
import { AnswerGridQuestion, AnswerGridRow } from '../../models/form.model';

interface HeaderGroup {
  isMatrix: boolean;
  sectionId: number;
  sectionName: string;
  questions: AnswerGridQuestion[];
}

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

  headerGroups = computed((): HeaderGroup[] => {
    const groups: HeaderGroup[] = [];
    for (const q of this.questions()) {
      const last = groups[groups.length - 1];
      if (last && last.isMatrix && q.isMatrix && q.sectionId === last.sectionId) {
        last.questions.push(q);
      } else {
        groups.push({ isMatrix: q.isMatrix, sectionId: q.sectionId, sectionName: q.sectionName, questions: [q] });
      }
    }
    return groups;
  });

  hasMatrixSections = computed(() => this.headerGroups().some(g => g.isMatrix));

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
