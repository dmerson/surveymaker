import { Component, OnInit, signal, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { SurveyService } from '../../services/survey.service';
import {
  LoadedQuestion, LoadedSection, LoadedSurvey,
  ParsedAttrs, ScoredOption, SurveyAnswerPayload, SurveyDetail
} from '../../models/survey.model';
import { evaluateFormula } from '../../utils/formula-evaluator';
import { SurveyChart } from '../../components/survey-chart/survey-chart';

// Question type IDs
const T = {
  TEXT: 1, LONG_TEXT: 2, NUMBER: 3,
  RADIO: 4, CHECKBOX: 5, DROPDOWN: 6,
  DATE: 7, TIME: 8, DATETIME: 9,
  IMAGE: 10, PDF: 11,
  GRAPH: 25,
  RATING: 12, LIKERT: 13, RANGE: 14,
  EMAIL: 15, PHONE: 16, URL: 17,
  NPS: 18, YES_NO: 19,
  CHECKBOX_VAL: 20, DROPDOWN_VAL: 21, RADIO_VAL: 22,
  CALCULATION: 24
} as const;

interface FileAnswer {
  fileName: string;
  contentType: string;
  dataBase64: string;
}

@Component({
  selector: 'app-take-survey',
  imports: [FormsModule, RouterLink, SurveyChart],
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
  previewMode    = signal(false);

  // answer map: questionId → string (scalar) or string[] (multi-select)
  answers     = signal<Record<number, string | string[]>>({});
  // free-text for "Other" option, keyed by questionId
  otherText   = signal<Record<number, string>>({});
  // file answers: questionId → FileAnswer
  fileAnswers = signal<Record<number, FileAnswer | null>>({});
  // error map: questionId → error message or null
  errors      = signal<Record<number, string | null>>({});

  submitting  = signal(false);
  submitted   = signal(false);
  submitError = signal('');

  ngOnInit(): void {
    const formId = this.route.snapshot.paramMap.get('id')!;
    this.previewMode.set(this.route.snapshot.queryParamMap.get('preview') === '1');

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

  // ── "Other" option helpers ────────────────────────────────────────────────

  isOtherOpt(opt: string): boolean {
    return opt.trim().toLowerCase() === 'other';
  }

  hasOtherSelected(q: LoadedQuestion): boolean {
    if (q.questionTypeId === T.RADIO || q.questionTypeId === T.DROPDOWN) {
      return this.isOtherOpt(this.strAnswer(q.questionId));
    }
    if (q.questionTypeId === T.CHECKBOX) {
      return this.arrAnswer(q.questionId).some(o => this.isOtherOpt(o));
    }
    return false;
  }

  getOtherText(questionId: number): string {
    return this.otherText()[questionId] ?? '';
  }

  setOtherText(questionId: number, text: string): void {
    this.otherText.update(m => ({ ...m, [questionId]: text }));
  }

  // ── File upload helpers ───────────────────────────────────────────────────

  getFileAnswer(questionId: number): FileAnswer | null {
    return this.fileAnswers()[questionId] ?? null;
  }

  onFileSelected(questionId: number, event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      const dataUrl = reader.result as string;
      const base64 = dataUrl.split(',')[1];
      this.fileAnswers.update(m => ({
        ...m, [questionId]: { fileName: file.name, contentType: file.type, dataBase64: base64 }
      }));
      this.errors.update(e => ({ ...e, [questionId]: null }));
    };
    reader.readAsDataURL(file);
  }

  removeFile(questionId: number): void {
    this.fileAnswers.update(m => ({ ...m, [questionId]: null }));
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

  calcValue(q: LoadedQuestion): string {
    const tokens = q.attrs.tokens;
    if (!tokens || tokens.length === 0) return '—';
    const survey = this.loadedSurvey();
    if (!survey) return '—';

    const answerMap = this.answers();

    const getVal = (questionId: number): number => {
      const ref = survey.allQuestions.find(rq => rq.questionId === questionId);
      if (!ref) return NaN;
      const raw = answerMap[questionId];

      switch (ref.questionTypeId) {
        case T.CHECKBOX_VAL: {
          const selected = (raw as string[]) ?? [];
          const opts = (ref.attrs.options as ScoredOption[]) ?? [];
          return selected.reduce((sum, text) => {
            const opt = opts.find(o => o.text === text);
            return sum + (opt?.value ?? 0);
          }, 0);
        }
        case T.DROPDOWN_VAL:
        case T.RADIO_VAL: {
          const text = (raw as string) ?? '';
          const opts = (ref.attrs.options as ScoredOption[]) ?? [];
          const opt = opts.find(o => o.text === text);
          return opt?.value ?? NaN;
        }
        default: {
          const n = parseFloat((raw as string) ?? '');
          return isNaN(n) ? 0 : n;
        }
      }
    };

    const result = evaluateFormula(tokens, getVal);
    if (isNaN(result) || !isFinite(result)) return '—';
    return String(Math.round(result * 10000) / 10000);
  }

  // ── Graph helpers ─────────────────────────────────────────────────────────

  private getNumericAnswer(questionId: number): number {
    const survey = this.loadedSurvey();
    if (!survey) return 0;
    const ref = survey.allQuestions.find(rq => rq.questionId === questionId);
    if (!ref) return 0;
    const raw = this.answers()[questionId];
    switch (ref.questionTypeId) {
      case T.CHECKBOX_VAL: {
        const selected = (raw as string[]) ?? [];
        const opts = (ref.attrs.options as ScoredOption[]) ?? [];
        return selected.reduce((sum, text) => {
          const o = opts.find(o => o.text === text);
          return sum + (o?.value ?? 0);
        }, 0);
      }
      case T.DROPDOWN_VAL:
      case T.RADIO_VAL: {
        const text = (raw as string) ?? '';
        const opts = (ref.attrs.options as ScoredOption[]) ?? [];
        const o = opts.find(o => o.text === text);
        return o?.value ?? 0;
      }
      case T.CALCULATION: {
        const v = parseFloat(this.calcValue(ref));
        return isNaN(v) ? 0 : v;
      }
      default: {
        const n = parseFloat((raw as string) ?? '');
        return isNaN(n) ? 0 : n;
      }
    }
  }

  graphLabels(q: LoadedQuestion): string[] {
    const survey = this.loadedSurvey();
    if (!survey) return [];
    return (q.attrs.sourceQuestionIds ?? []).map(id => {
      const ref = survey.allQuestions.find(rq => rq.questionId === id);
      const txt = ref?.text ?? `Q${id}`;
      return txt.length > 24 ? txt.slice(0, 24) + '…' : txt;
    });
  }

  graphValues(q: LoadedQuestion): number[] {
    void this.answers(); // track signal for reactivity
    return (q.attrs.sourceQuestionIds ?? []).map(id => this.getNumericAnswer(id));
  }

  // ── Validation ────────────────────────────────────────────────────────────

  onBlur(q: LoadedQuestion): void {
    const err = this.validate(q);
    this.errors.update(e => ({ ...e, [q.questionId]: err }));
  }

  private validate(q: LoadedQuestion): string | null {
    if (q.questionTypeId === T.IMAGE || q.questionTypeId === T.PDF) {
      if (q.attrs.required && !this.fileAnswers()[q.questionId])
        return 'Please upload a file.';
      return null;
    }

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

      case T.RADIO:
      case T.DROPDOWN:
        if (this.isOtherOpt(val) && !this.getOtherText(q.questionId).trim())
          return 'Please specify your answer for "Other".';
        break;

      case T.CHECKBOX:
        if ((raw as string[]).some(o => this.isOtherOpt(o)) && !this.getOtherText(q.questionId).trim())
          return 'Please specify your answer for "Other".';
        break;
    }
    return null;
  }

  // ── Submit ────────────────────────────────────────────────────────────────

  submitSurvey(): void {
    const survey = this.loadedSurvey();
    if (!survey) return;

    // Validate all questions and collect errors (skip computed calculation questions)
    const newErrors: Record<number, string | null> = {};
    let hasErrors = false;
    for (const q of survey.allQuestions) {
      if (q.questionTypeId === T.CALCULATION || q.questionTypeId === T.GRAPH) continue;
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
        // Calculation questions: store the evaluated result
        if (q.questionTypeId === T.CALCULATION) {
          const val = this.calcValue(q);
          return val !== '—' ? { questionId: q.questionId, answerScalar: val } : null;
        }
        // Graph questions: display only, no answer stored
        if (q.questionTypeId === T.GRAPH) return null;
        // File questions: base64 payload
        if (q.questionTypeId === T.IMAGE || q.questionTypeId === T.PDF) {
          const fa = this.fileAnswers()[q.questionId];
          if (!fa) return null;
          return {
            questionId: q.questionId,
            answerJson: JSON.stringify({ fileName: fa.fileName, contentType: fa.contentType, data: fa.dataBase64 })
          };
        }
        const raw = answerMap[q.questionId];
        if (raw == null || raw === '' || (Array.isArray(raw) && raw.length === 0)) {
          return null;
        }
        if (Array.isArray(raw)) {
          // Replace 'Other' with the typed text for checkbox lists
          const resolved = raw.map(v =>
            this.isOtherOpt(v) ? (this.getOtherText(q.questionId) || v) : v
          );
          return { questionId: q.questionId, answerJson: JSON.stringify(resolved) };
        }
        // Replace 'Other' with the typed text for radio / dropdown
        const scalar = (q.questionTypeId === T.RADIO || q.questionTypeId === T.DROPDOWN)
          && this.isOtherOpt(raw)
          ? (this.getOtherText(q.questionId) || raw)
          : raw;
        return { questionId: q.questionId, answerScalar: scalar };
      })
      .filter((a): a is SurveyAnswerPayload => a !== null);

    this.submitting.set(true);
    this.submitError.set('');

    this.surveyService.submit(survey.formId, payload).subscribe({
      next: () => { this.submitting.set(false); this.submitted.set(true); },
      error: (err) => {
        this.submitting.set(false);
        const msg = err?.error?.error;
        this.submitError.set(msg ?? 'Failed to submit. Please try again.');
      }
    });
  }
}
