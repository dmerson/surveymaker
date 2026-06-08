import { Component, OnInit, input, output, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SlicePipe } from '@angular/common';
import { QuestionDetail } from '../../models/form.model';
import {
  ConditionalLogicConfig, ConditionOperator, ThenAction, ThenActionType
} from '../../models/survey.model';

const EXCLUDED_TYPES = new Set([0, 26]); // Instruction and Conditional Logic

export const OPERATORS: { value: ConditionOperator; label: string }[] = [
  { value: 'eq',  label: '= (equals)' },
  { value: 'neq', label: '≠ (not equals)' },
  { value: 'gt',  label: '> (greater than)' },
  { value: 'lt',  label: '< (less than)' },
  { value: 'gte', label: '≥ (greater than or equal)' },
  { value: 'lte', label: '≤ (less than or equal)' },
];

export const ACTIONS: { value: ThenActionType; label: string }[] = [
  { value: 'show',    label: 'Show' },
  { value: 'hide',    label: 'Hide' },
  { value: 'require', label: 'Require' },
];

@Component({
  selector: 'app-conditional-logic-wizard',
  imports: [FormsModule, SlicePipe],
  templateUrl: './conditional-logic-wizard.html',
  styleUrl: './conditional-logic-wizard.scss'
})
export class ConditionalLogicWizard implements OnInit {
  questions     = input.required<QuestionDetail[]>();
  initialConfig = input<ConditionalLogicConfig | null>(null);

  saved     = output<ConditionalLogicConfig>();
  cancelled = output<void>();

  readonly operators = OPERATORS;
  readonly actions   = ACTIONS;

  // IF state
  sourceQuestionId = signal<number | null>(null);
  operator         = signal<ConditionOperator>('eq');
  value            = signal('');

  // THEN state
  thenActions      = signal<ThenAction[]>([]);
  pendingTargetId  = signal<number | null>(null);
  pendingAction    = signal<ThenActionType>('show');

  error = signal('');

  eligibleQuestions = computed(() =>
    this.questions().filter(q => !EXCLUDED_TYPES.has(q.questionTypeId))
  );

  availableTargets = computed(() => {
    const srcId = this.sourceQuestionId();
    const usedIds = new Set(this.thenActions().map(a => a.questionId));
    return this.eligibleQuestions().filter(
      q => q.questionId !== srcId && !usedIds.has(q.questionId)
    );
  });

  sourceQuestion = computed(() =>
    this.eligibleQuestions().find(q => q.questionId === this.sourceQuestionId()) ?? null
  );

  ngOnInit(): void {
    const cfg = this.initialConfig();
    if (cfg) {
      this.sourceQuestionId.set(cfg.condition.questionId);
      this.operator.set(cfg.condition.operator);
      this.value.set(cfg.condition.value);
      this.thenActions.set([...cfg.thenActions]);
    }
    // seed pending target to first available
    const first = this.availableTargets()[0];
    if (first) this.pendingTargetId.set(first.questionId);
  }

  addAction(): void {
    const targetId = this.pendingTargetId();
    if (targetId === null) return;
    this.thenActions.update(list => [...list, { questionId: targetId, action: this.pendingAction() }]);
    // advance pending to next available
    const next = this.availableTargets()[0];
    this.pendingTargetId.set(next?.questionId ?? null);
  }

  removeAction(index: number): void {
    this.thenActions.update(list => list.filter((_, i) => i !== index));
    if (this.pendingTargetId() === null) {
      const next = this.availableTargets()[0];
      if (next) this.pendingTargetId.set(next.questionId);
    }
  }

  questionLabel(id: number): string {
    const q = this.eligibleQuestions().find(q => q.questionId === id);
    return q ? `Q${q.order}: ${q.text.slice(0, 50)}` : `#${id}`;
  }

  actionLabel(a: ThenActionType): string {
    return ACTIONS.find(x => x.value === a)?.label ?? a;
  }

  save(): void {
    this.error.set('');
    if (this.sourceQuestionId() === null) { this.error.set('Select a source question for the IF condition.'); return; }
    if (!this.value().trim()) { this.error.set('Enter a value to compare against.'); return; }
    if (this.thenActions().length === 0) { this.error.set('Add at least one THEN action.'); return; }

    this.saved.emit({
      condition: {
        questionId: this.sourceQuestionId()!,
        operator:   this.operator(),
        value:      this.value().trim(),
      },
      thenActions: this.thenActions(),
    });
  }

  cancel(): void { this.cancelled.emit(); }
}
