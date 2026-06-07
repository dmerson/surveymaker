import { Component, OnInit, signal, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { SurveyService } from '../../services/survey.service';
import {
  LoadedQuestion, LoadedSection, LoadedSurvey,
  ParsedAttrs, ScoredOption, SurveyAnswerPayload, SurveyDetail
} from '../../models/survey.model';

// Question type IDs
const T = {
  TEXT: 1, LONG_TEXT: 2, NUMBER: 3,
  RADIO: 4, CHECKBOX: 5, DROPDOWN: 6,
  DATE: 7, TIME: 8, DATETIME: 9,
  RATING: 12, LIKERT: 13, RANGE: 14,
  EMAIL: 15, PHONE: 16, URL: 17,
  NPS: 18, YES_NO: 19,
  CHECKBOX_VAL: 20, DROPDOWN_VAL: 21, RADIO_VAL: 22
} as const;

@Component({
  selector: 'app-take-survey',
  imports: [FormsModule, RouterLink],
  templateUrl: './take-survey.html',
  styleUrl: './take-survey.scss'
})
export class TakeSurvey implements OnInit {
  private readonly route         = inject(ActivatedRoute);
  private readonly surveyService = inject(SurveyService);

  // Expose type constants to the template
  readonly T = T;

  loadedSurvey   = signal<LoadedSurvey | null>(null);
  loading        = signal(true);
  loadError      = signal('');
  accessDenied   = signal(false);

  // answer map: questionId → string (scalar) or string[] (multi-select)
  answers     = signal<Record<number, string | string[]>>({});
  // error map: questionId → error message or null
  errors      = signal<Record<number, string | null>>({});

  submitting  = signal(false);
  submitted   = signal(false);
  submitError = signal('');

  ngOnInit(): void {
    const formId = this.route.snapshot.paramMap.get('id')!;

    this.surveyService.getSurvey(formId).subscribe({
      next: detail => {
        this.loadedSurvey.set(this.loadSurvey(detail));
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        if (err?.status === 403) {
          this.accessDenied.set(true);
        } else {
          this.loadError.set('Survey not found or no longer available.');
        }
      }
    });
  }

  private loadSurvey(detail: SurveyDetail): LoadedSurvey {
    const allQuestions: LoadedQuestion[] = [];
    const sections: LoadedSection[] = detail.sections.map(s => {
      const questions: LoadedQuestion[] = s.questions.map(q => {
        const loaded: LoadedQuestion = { ...q, attrs: this.parseAttrsJson(q.questionAttributes) };
        allQuestions.push(loaded);
        return loaded;
      });
      return { ...s, questions };
    });
    return { ...detail, sections, allQuestions };
  }

  private parseAttrsJson(json: string | undefined): ParsedAttrs {
    if (!json || json === '{}') return {};
    try { return JSON.parse(json) as ParsedAttrs; }
    catch { return {}; }
  }

  // ── Answer helpers ────────────────────────────────────────────────────────

  strAnswer(questionId: number): string {
    return (this.answers()[questionId] as string) ?? '';
  }

  arrAnswer(questionId: number): string[] {
    return (this.answers()[questionId] as string[]) ?? [];
  }

  setAnswer(questionId: number, value: string): void {
    this.answers.update(a => ({ ...a, [questionId]: value }));
  }

  toggleCheckbox(questionId: number, option: string, checked: boolean): void {
    this.answers.update(a => {
      const current: string[] = (a[questionId] as string[]) ?? [];
      const updated = checked ? [...current, option] : current.filter(o => o !== option);
      return { ...a, [questionId]: updated };
    });
  }

  isChecked(questionId: number, option: string): boolean {
    return this.arrAnswer(questionId).includes(option);
  }

  // ── Type-specific helpers (used in template) ───────────────────────────────

  strOpts(q: LoadedQuestion): string[] {
    return (q.attrs.options as string[]) ?? [];
  }

  scoredOpts(q: LoadedQuestion): ScoredOption[] {
    return (q.attrs.options as ScoredOption[]) ?? [];
  }

  scaleItems(q: LoadedQuestion): number[] {
    const len = q.attrs.scale ?? 5;
    return Array.from({ length: len }, (_, i) => i + 1);
  }

  ratingItems(q: LoadedQuestion): number[] {
    const min = q.attrs.min ?? 1;
    const max = q.attrs.max ?? 5;
    return Array.from({ length: max - min + 1 }, (_, i) => i + min);
  }

  npsItems(): number[] {
    return [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
  }

  rangeMin(q: LoadedQuestion): number { return q.attrs.min ?? 0; }
  rangeMax(q: LoadedQuestion): number { return q.attrs.max ?? 100; }
  rangeVal(q: LoadedQuestion): number {
    const v = parseFloat(this.strAnswer(q.questionId));
    return isNaN(v) ? this.rangeMin(q) : v;
  }

  // ── Validation ────────────────────────────────────────────────────────────

  onBlur(q: LoadedQuestion): void {
    const err = this.validate(q);
    this.errors.update(e => ({ ...e, [q.questionId]: err }));
  }

  private validate(q: LoadedQuestion): string | null {
    const raw = this.answers()[q.questionId];
    const { required, min, max } = q.attrs;
    const isEmpty = raw == null || raw === ''
      || (Array.isArray(raw) && raw.length === 0);

    if (required && isEmpty) return 'This field is required.';
    if (isEmpty) return null;  // non-required and empty → no error

    const val = Array.isArray(raw) ? '' : raw;

    switch (q.questionTypeId) {
      case T.TEXT:
      case T.LONG_TEXT:
        if (min != null && val.length < min) return `Minimum ${min} characters required.`;
        if (max != null && val.length > max) return `Maximum ${max} characters allowed.`;
        break;

      case T.NUMBER:
      case T.RANGE: {
        const n = parseFloat(val);
        if (!isNaN(n)) {
          if (min != null && n < min) return `Minimum value is ${min}.`;
          if (max != null && n > max) return `Maximum value is ${max}.`;
        }
        break;
      }

      case T.EMAIL:
        if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(val))
          return 'Please enter a valid email address.';
        break;

      case T.PHONE:
        if (!/^[+]?[(]?[0-9]{1,4}[)]?[-\s.]?[(]?[0-9]{1,4}[)]?[-\s.]?[0-9]{4,10}$/.test(val.replace(/\s/g, '')))
          return 'Please enter a valid phone number.';
        break;

      case T.URL:
        try { new URL(val); }
        catch { return 'Please enter a valid URL (e.g. https://example.com).'; }
        break;
    }
    return null;
  }

  // ── Submit ────────────────────────────────────────────────────────────────

  submitSurvey(): void {
    const survey = this.loadedSurvey();
    if (!survey) return;

    // Validate all questions and collect errors
    const newErrors: Record<number, string | null> = {};
    let hasErrors = false;
    for (const q of survey.allQuestions) {
      const err = this.validate(q);
      newErrors[q.questionId] = err;
      if (err) hasErrors = true;
    }
    this.errors.set(newErrors);

    if (hasErrors) {
      // Scroll to first error
      setTimeout(() => {
        const el = document.querySelector('.has-error');
        el?.scrollIntoView({ behavior: 'smooth', block: 'center' });
      }, 50);
      return;
    }

    const answerMap = this.answers();
    const payload = survey.allQuestions
      .map((q): SurveyAnswerPayload | null => {
        const raw = answerMap[q.questionId];
        if (raw == null || raw === '' || (Array.isArray(raw) && raw.length === 0)) {
          return null;
        }
        if (Array.isArray(raw)) {
          return { questionId: q.questionId, answerJson: JSON.stringify(raw) };
        }
        return { questionId: q.questionId, answerScalar: raw };
      })
      .filter((a): a is SurveyAnswerPayload => a !== null);

    this.submitting.set(true);
    this.submitError.set('');

    this.surveyService.submit(survey.formId, payload).subscribe({
      next: () => { this.submitting.set(false); this.submitted.set(true); },
      error: () => {
        this.submitting.set(false);
        this.submitError.set('Failed to submit. Please try again.');
      }
    });
  }
}
