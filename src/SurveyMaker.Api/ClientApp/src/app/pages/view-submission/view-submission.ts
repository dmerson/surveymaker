import { Component, OnInit, signal, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { SlicePipe } from '@angular/common';
import { FormService } from '../../services/form.service';
import {
  SubmissionDetail, SubmissionSectionDetail, SubmissionQuestionDetail
} from '../../models/form.model';
import { SurveyChart } from '../../components/survey-chart/survey-chart';
import { GraphType, ParsedAttrs, ScoredOption } from '../../models/survey.model';

const T = {
  TEXT: 1, LONG_TEXT: 2, NUMBER: 3,
  RADIO: 4, CHECKBOX: 5, DROPDOWN: 6,
  DATE: 7, TIME: 8, DATETIME: 9,
  IMAGE: 10, PDF: 11,
  RATING: 12, LIKERT: 13, RANGE: 14,
  EMAIL: 15, PHONE: 16, URL: 17,
  NPS: 18, YES_NO: 19,
  CHECKBOX_VAL: 20, DROPDOWN_VAL: 21, RADIO_VAL: 22,
  CALCULATION: 24,
  GRAPH: 25
} as const;


interface LoadedQuestion extends SubmissionQuestionDetail {
  attrs: ParsedAttrs;
  answerArr: string[];
}

interface LoadedSection extends Omit<SubmissionSectionDetail, 'questions'> {
  questions: LoadedQuestion[];
}

interface LoadedSubmission extends Omit<SubmissionDetail, 'sections'> {
  sections: LoadedSection[];
  allQuestions: LoadedQuestion[];
}

@Component({
  selector: 'app-view-submission',
  imports: [RouterLink, SlicePipe, SurveyChart],
  templateUrl: './view-submission.html',
  styleUrl: './view-submission.scss'
})
export class ViewSubmission implements OnInit {
  private readonly route       = inject(ActivatedRoute);
  private readonly formService = inject(FormService);

  readonly T = T;

  submission = signal<LoadedSubmission | null>(null);
  loading    = signal(true);
  error      = signal('');

  ngOnInit(): void {
    const formId       = this.route.snapshot.paramMap.get('id')!;
    const submissionId = this.route.snapshot.paramMap.get('submissionId')!;

    this.formService.getSubmission(formId, submissionId).subscribe({
      next: detail => {
        this.submission.set(this.process(detail));
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Submission not found.');
        this.loading.set(false);
      }
    });
  }

  private process(detail: SubmissionDetail): LoadedSubmission {
    const allQuestions: LoadedQuestion[] = [];
    const sections: LoadedSection[] = detail.sections.map(s => ({
      ...s,
      questions: s.questions.map(q => {
        const lq: LoadedQuestion = {
          ...q,
          attrs:     this.parseAttrs(q.questionAttributes),
          answerArr: q.answerJson ? (JSON.parse(q.answerJson) as string[]) : []
        };
        allQuestions.push(lq);
        return lq;
      })
    }));
    return { ...detail, sections, allQuestions };
  }

  private parseAttrs(json: string | undefined): ParsedAttrs {
    if (!json || json === '{}') return {};
    try { return JSON.parse(json) as ParsedAttrs; }
    catch { return {}; }
  }

  strOpts(q: LoadedQuestion): string[] {
    return (q.attrs.options as string[]) ?? [];
  }

  scoredOpts(q: LoadedQuestion): ScoredOption[] {
    return (q.attrs.options as ScoredOption[]) ?? [];
  }

  ratingItems(q: LoadedQuestion): number[] {
    const min = q.attrs.min ?? 1, max = q.attrs.max ?? 5;
    return Array.from({ length: max - min + 1 }, (_, i) => i + min);
  }

  scaleItems(q: LoadedQuestion): number[] {
    return Array.from({ length: q.attrs.scale ?? 5 }, (_, i) => i + 1);
  }

  npsItems(): number[] { return [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10]; }

  rangeMin(q: LoadedQuestion): number { return q.attrs.min ?? 0; }
  rangeMax(q: LoadedQuestion): number { return q.attrs.max ?? 100; }

  graphLabels(q: LoadedQuestion, sub: LoadedSubmission): string[] {
    return (q.attrs.sourceQuestionIds ?? []).map(id => {
      const ref = sub.allQuestions.find(rq => rq.questionId === id);
      const txt = ref?.text ?? `Q${id}`;
      return txt.length > 24 ? txt.slice(0, 24) + '…' : txt;
    });
  }

  graphValues(q: LoadedQuestion, sub: LoadedSubmission): number[] {
    return (q.attrs.sourceQuestionIds ?? []).map(id => {
      const ref = sub.allQuestions.find(rq => rq.questionId === id);
      if (!ref) return 0;
      const n = parseFloat(ref.answerScalar ?? '');
      return isNaN(n) ? 0 : n;
    });
  }

  rangePercent(q: LoadedQuestion): number {
    if (!q.answerScalar) return 0;
    const val = parseFloat(q.answerScalar);
    const min = this.rangeMin(q), max = this.rangeMax(q);
    return Math.max(0, Math.min(100, ((val - min) / (max - min)) * 100));
  }
}
