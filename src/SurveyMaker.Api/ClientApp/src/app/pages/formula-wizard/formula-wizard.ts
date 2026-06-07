import { Component, OnInit, input, output, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { QuestionDetail } from '../../models/form.model';
import { FormulaToken } from '../../models/survey.model';
import { evaluateFormula } from '../../utils/formula-evaluator';

const NUMERIC = new Set([3, 12, 13, 14, 18, 20, 21, 22]);

@Component({
  selector: 'app-formula-wizard',
  imports: [FormsModule],
  templateUrl: './formula-wizard.html',
  styleUrl: './formula-wizard.scss'
})
export class FormulaWizard implements OnInit {
  questions     = input.required<QuestionDetail[]>();
  initialTokens = input<FormulaToken[]>([]);

  saved     = output<FormulaToken[]>();
  cancelled = output<void>();

  tokens   = signal<FormulaToken[]>([]);
  numInput = signal('');

  numericQuestions = computed(() =>
    this.questions().filter(q => NUMERIC.has(q.questionTypeId))
  );

  formulaText = computed(() =>
    this.tokens().map(t => {
      switch (t.type) {
        case 'question': return `[${t.label}]`;
        case 'op':       return ` ${t.value} `;
        case 'fn':       return t.value;
        case 'paren':    return t.value;
        case 'number':   return String(t.value);
      }
    }).join('')
  );

  previewResult = computed(() => {
    const toks = this.tokens();
    if (toks.length === 0) return '';
    const result = evaluateFormula(toks, () => 1);
    if (isNaN(result)) return 'Invalid';
    return String(Math.round(result * 10000) / 10000);
  });

  ngOnInit(): void {
    this.tokens.set([...this.initialTokens()]);
  }

  insertOp(value: '+' | '-' | '*' | '/' | 'mod' | ','): void {
    this.tokens.update(t => [...t, { type: 'op', value }]);
  }

  insertFn(value: 'sqrt' | 'abs' | 'mean' | 'max' | 'min'): void {
    this.tokens.update(t => [
      ...t,
      { type: 'fn', value },
      { type: 'paren', value: '(' as const }
    ]);
  }

  insertParen(value: '(' | ')'): void {
    this.tokens.update(t => [...t, { type: 'paren', value }]);
  }

  insertQuestion(q: QuestionDetail): void {
    this.tokens.update(t => [...t, { type: 'question', id: q.questionId, label: q.text }]);
  }

  insertNumber(): void {
    const n = parseFloat(this.numInput());
    if (isNaN(n)) return;
    this.tokens.update(t => [...t, { type: 'number', value: n }]);
    this.numInput.set('');
  }

  removeToken(i: number): void {
    this.tokens.update(t => t.filter((_, idx) => idx !== i));
  }

  deleteLast(): void {
    this.tokens.update(t => t.slice(0, -1));
  }

  clearAll(): void {
    this.tokens.set([]);
  }

  save(): void {
    this.saved.emit(this.tokens());
  }

  cancel(): void {
    this.cancelled.emit();
  }

  tokenLabel(t: FormulaToken): string {
    switch (t.type) {
      case 'question': return `[${t.label}]`;
      case 'op':       return t.value;
      case 'fn':       return t.value + '(';
      case 'paren':    return t.value;
      case 'number':   return String(t.value);
    }
  }

  readonly ops: Array<'+' | '-' | '*' | '/' | 'mod' | ','> = ['+', '-', '*', '/', 'mod', ','];
  readonly fns: Array<'sqrt' | 'abs' | 'mean' | 'max' | 'min'> = ['sqrt', 'abs', 'mean', 'max', 'min'];
}
