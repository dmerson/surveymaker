import { Component, OnInit, computed, signal, inject } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FormService } from '../../services/form.service';
import { FormDetail, QuestionDetail, QuestionType, SectionDetail } from '../../models/form.model';
import { FormulaToken, GraphType } from '../../models/survey.model';
import { FormulaWizard } from '../formula-wizard/formula-wizard';
import { GraphWizard } from '../graph-wizard/graph-wizard';

@Component({
  selector: 'app-form-editor',
  imports: [FormsModule, RouterLink, NgTemplateOutlet, FormulaWizard, GraphWizard],
  templateUrl: './form-editor.html',
  styleUrl: './form-editor.scss'
})
export class FormEditor implements OnInit {
  // ── Data ─────────────────────────────────────────────────────────────────
  form          = signal<FormDetail | null>(null);
  questionTypes = signal<QuestionType[]>([]);
  loading       = signal(true);
  loadError     = signal('');

  // ── Add/Edit Question state ───────────────────────────────────────────────
  aqSectionId         = signal<number | null>(null);  // null = panel closed
  aqEditingQuestionId = signal<number | null>(null);  // null = adding new
  aqTypeId     = signal(0);
  aqText       = signal('');
  aqOrder      = signal(1);
  aqRequired   = signal(false);
  // text types
  aqMinLen     = signal('');
  aqMaxLen     = signal('');
  // numeric/rating types
  aqMinVal     = signal('');
  aqMaxVal     = signal('');
  // option-list types
  aqOptions    = signal<string[]>([]);
  aqPendOpt    = signal('');
  // scored option types (Checkbox/Radio/Dropdown with values)
  aqScored     = signal<{ text: string; value: string }[]>([]);
  aqPendText   = signal('');
  aqPendVal    = signal('');
  // scale types (Likert)
  aqScale      = signal(5);
  // formula types (Calculation)
  aqTokens     = signal<FormulaToken[]>([]);
  showFormulaWizard = signal(false);
  // graph types (Graph)
  aqGraphType      = signal<GraphType>('bar');
  aqGraphSourceIds = signal<number[]>([]);
  showGraphWizard  = signal(false);
  // status
  aqSaving     = signal(false);
  aqError      = signal('');

  // ── Add Section state ─────────────────────────────────────────────────────
  asOpen   = signal(false);
  asName   = signal('');
  asOrder  = signal(1);
  asSaving = signal(false);
  asError  = signal('');

  // ── Form settings (editable) ──────────────────────────────────────────────
  fsFormName       = signal('');
  fsDescription    = signal('');
  fsPublished      = signal(false);
  fsRandomize      = signal(false);
  fsQuota          = signal('');
  fsSecurityTypeId = signal(1);
  fsSaving         = signal(false);
  fsSaved          = signal(false);
  fsError          = signal('');

  // ── Delete form ───────────────────────────────────────────────────────────
  showDeleteConfirm = signal(false);
  deleting          = signal(false);

  // ── Computed ──────────────────────────────────────────────────────────────
  hasAnyQuestion = computed(() =>
    (this.form()?.sections ?? []).some(s => s.questions.length > 0)
  );

  typeFlags = computed(() => {
    const id = this.aqTypeId();
    return {
      hasMinMax:      [1, 2].includes(id),
      hasNumberRange: [3, 12, 14].includes(id),
      hasOptions:     [4, 5, 6].includes(id),
      hasScored:      [20, 21, 22].includes(id),
      hasScale:       [13].includes(id),
      isFormula:      id === 24,
      isGraph:        id === 25,
    };
  });

  filteredQuestionTypes = computed(() => {
    const types = this.questionTypes();
    if (this.fsSecurityTypeId() === 2) return types;
    return types.filter(t => t.questionTypeId !== 10 && t.questionTypeId !== 11);
  });

  private static readonly NUMERIC_TYPE_IDS      = new Set([3, 12, 13, 14, 18, 20, 21, 22]);
  private static readonly GRAPH_SOURCE_TYPE_IDS = new Set([3, 12, 13, 14, 18, 20, 21, 22, 24]);

  calcAvailableQuestions = computed(() => {
    const f = this.form();
    if (!f) return [];
    const editId = this.aqEditingQuestionId();
    return f.sections
      .flatMap(s => s.questions)
      .filter(q => FormEditor.NUMERIC_TYPE_IDS.has(q.questionTypeId) && q.questionId !== editId);
  });

  graphAvailableQuestions = computed(() => {
    const f = this.form();
    if (!f) return [];
    const editId = this.aqEditingQuestionId();
    return f.sections
      .flatMap(s => s.questions)
      .filter(q => FormEditor.GRAPH_SOURCE_TYPE_IDS.has(q.questionTypeId) && q.questionId !== editId);
  });

  graphPreview = computed(() => {
    const ids = this.aqGraphSourceIds();
    if (ids.length < 2) return 'No graph configured';
    const type = this.aqGraphType();
    const f = this.form();
    if (!f) return `${type} chart — ${ids.length} questions`;
    const qs = f.sections.flatMap(s => s.questions);
    const names = ids.map(id => qs.find(q => q.questionId === id)?.text ?? `Q${id}`).join(', ');
    return `${type.charAt(0).toUpperCase() + type.slice(1)} chart — ${names}`;
  });

  formulaPreview = computed(() => {
    const toks = this.aqTokens();
    if (toks.length === 0) return 'No formula defined';
    return toks.map(t => {
      switch (t.type) {
        case 'question': return `[${t.label}]`;
        case 'op':       return ` ${t.value} `;
        case 'fn':       return t.value;
        case 'paren':    return t.value;
        case 'number':   return String(t.value);
      }
    }).join('');
  });

  private readonly route       = inject(ActivatedRoute);
  private readonly router      = inject(Router);
  private readonly formService = inject(FormService);

  ngOnInit(): void {
    const formId = this.route.snapshot.paramMap.get('id')!;

    this.formService.getDetail(formId).subscribe({
      next: detail => {
        this.form.set(detail);
        this.fsFormName.set(detail.formName);
        this.fsDescription.set(detail.description ?? '');
        this.fsPublished.set(detail.published);
        this.fsRandomize.set(detail.randomizeOrder);
        this.fsQuota.set(detail.quota ? String(detail.quota) : '');
        this.fsSecurityTypeId.set(detail.securityTypeId);
        if (detail.sections.length > 0) {
          this.openAddPanel(detail.sections[0]);
        }
        this.loading.set(false);
      },
      error: () => {
        this.loadError.set('Could not load form.');
        this.loading.set(false);
      }
    });

    this.formService.getQuestionTypes().subscribe({
      next: types => this.questionTypes.set(types)
    });
  }

  // ── Add / Edit Question ───────────────────────────────────────────────────

  openAddPanel(section: SectionDetail): void {
    this.aqSectionId.set(section.sectionId);
    this.aqEditingQuestionId.set(null);
    this.aqOrder.set(section.questions.length + 1);
    this.resetAqFields();
  }

  editQuestion(section: SectionDetail, q: QuestionDetail): void {
    this.aqSectionId.set(section.sectionId);
    this.aqEditingQuestionId.set(q.questionId);
    this.aqTypeId.set(q.questionTypeId);
    this.aqText.set(q.text);
    this.aqOrder.set(q.order);
    this.aqRequired.set(false);
    this.aqMinLen.set(''); this.aqMaxLen.set('');
    this.aqMinVal.set(''); this.aqMaxVal.set('');
    this.aqOptions.set([]); this.aqPendOpt.set('');
    this.aqScored.set([]); this.aqPendText.set(''); this.aqPendVal.set('');
    this.aqScale.set(5);
    this.aqError.set('');
    this.loadAttributes(q.questionAttributes, q.questionTypeId);
  }

  cancelEdit(): void {
    this.aqEditingQuestionId.set(null);
    this.resetAqFields();
  }

  private loadAttributes(attrsJson: string | undefined, typeId: number): void {
    if (!attrsJson || attrsJson === '{}') return;
    try {
      const attrs = JSON.parse(attrsJson);
      if (attrs['required']) this.aqRequired.set(true);
      if ([1, 2].includes(typeId)) {
        if (attrs['min'] != null) this.aqMinLen.set(String(attrs['min']));
        if (attrs['max'] != null) this.aqMaxLen.set(String(attrs['max']));
      }
      if ([3, 12, 14].includes(typeId)) {
        if (attrs['min'] != null) this.aqMinVal.set(String(attrs['min']));
        if (attrs['max'] != null) this.aqMaxVal.set(String(attrs['max']));
      }
      if ([4, 5, 6].includes(typeId) && Array.isArray(attrs['options'])) {
        this.aqOptions.set(attrs['options'] as string[]);
      }
      if ([20, 21, 22].includes(typeId) && Array.isArray(attrs['options'])) {
        this.aqScored.set(
          (attrs['options'] as { text: string; value: number }[])
            .map(o => ({ text: o.text, value: String(o.value) }))
        );
      }
      if (typeId === 13 && attrs['scale'] != null) {
        this.aqScale.set(attrs['scale'] as number);
      }
      if (typeId === 24 && Array.isArray(attrs['tokens'])) {
        this.aqTokens.set(attrs['tokens'] as FormulaToken[]);
      }
      if (typeId === 25) {
        if (attrs['graphType']) this.aqGraphType.set(attrs['graphType'] as GraphType);
        if (Array.isArray(attrs['sourceQuestionIds'])) this.aqGraphSourceIds.set(attrs['sourceQuestionIds'] as number[]);
      }
    } catch { /* ignore invalid JSON */ }
  }

  private resetAqFields(): void {
    this.aqTypeId.set(0);
    this.aqText.set('');
    this.aqRequired.set(false);
    this.aqMinLen.set(''); this.aqMaxLen.set('');
    this.aqMinVal.set(''); this.aqMaxVal.set('');
    this.aqOptions.set([]); this.aqPendOpt.set('');
    this.aqScored.set([]); this.aqPendText.set(''); this.aqPendVal.set('');
    this.aqScale.set(5);
    this.aqTokens.set([]);
    this.aqGraphType.set('bar');
    this.aqGraphSourceIds.set([]);
    this.aqError.set('');
  }

  onTypeChange(typeId: number): void {
    this.aqTypeId.set(+typeId);
    this.aqMinLen.set(''); this.aqMaxLen.set('');
    this.aqMinVal.set(''); this.aqMaxVal.set('');
    this.aqOptions.set([]); this.aqPendOpt.set('');
    this.aqScored.set([]); this.aqPendText.set(''); this.aqPendVal.set('');
    this.aqScale.set(5);
    this.aqTokens.set([]);
    this.aqGraphType.set('bar');
    this.aqGraphSourceIds.set([]);
    this.aqError.set('');
  }

  openFormulaWizard(): void {
    this.showFormulaWizard.set(true);
  }

  onFormulaSaved(tokens: FormulaToken[]): void {
    this.aqTokens.set(tokens);
    this.showFormulaWizard.set(false);
  }

  onFormulaWizardCancelled(): void {
    this.showFormulaWizard.set(false);
  }

  openGraphWizard(): void { this.showGraphWizard.set(true); }

  onGraphSaved(cfg: { graphType: GraphType; sourceQuestionIds: number[] }): void {
    this.aqGraphType.set(cfg.graphType);
    this.aqGraphSourceIds.set(cfg.sourceQuestionIds);
    this.showGraphWizard.set(false);
  }

  onGraphWizardCancelled(): void { this.showGraphWizard.set(false); }

  addOption(): void {
    const opt = this.aqPendOpt().trim();
    if (!opt) return;
    this.aqOptions.update(opts => [...opts, opt]);
    this.aqPendOpt.set('');
  }

  removeOption(i: number): void {
    this.aqOptions.update(opts => opts.filter((_, idx) => idx !== i));
  }

  addScoredOption(): void {
    const text = this.aqPendText().trim();
    // number inputs emit a number; String() makes .trim() safe
    const val  = String(this.aqPendVal()).trim();
    if (!text || !val) return;
    this.aqScored.update(opts => [...opts, { text, value: val }]);
    this.aqPendText.set(''); this.aqPendVal.set('');
  }

  removeScoredOption(i: number): void {
    this.aqScored.update(opts => opts.filter((_, idx) => idx !== i));
  }

  private buildAttributes(): string {
    const f = this.typeFlags();
    const attrs: Record<string, unknown> = {};

    if (this.aqRequired()) attrs['required'] = true;

    if (f.hasMinMax) {
      if (this.aqMinLen()) attrs['min'] = parseInt(String(this.aqMinLen()), 10);
      if (this.aqMaxLen()) attrs['max'] = parseInt(String(this.aqMaxLen()), 10);
    }
    if (f.hasNumberRange) {
      if (this.aqMinVal()) attrs['min'] = parseFloat(String(this.aqMinVal()));
      if (this.aqMaxVal()) attrs['max'] = parseFloat(String(this.aqMaxVal()));
    }
    if (f.hasOptions && this.aqOptions().length > 0) {
      attrs['options'] = this.aqOptions();
    }
    if (f.hasScored && this.aqScored().length > 0) {
      attrs['options'] = this.aqScored().map(o => ({
        text: o.text,
        value: parseFloat(o.value)
      }));
    }
    if (f.hasScale) {
      attrs['scale'] = this.aqScale();
    }
    if (f.isFormula && this.aqTokens().length > 0) {
      attrs['tokens'] = this.aqTokens();
    }
    if (f.isGraph && this.aqGraphSourceIds().length >= 2) {
      attrs['graphType']        = this.aqGraphType();
      attrs['sourceQuestionIds'] = this.aqGraphSourceIds();
    }

    // Always store valid JSON; never null
    return JSON.stringify(attrs);
  }

  saveQuestion(): void {
    if (!this.aqTypeId()) { this.aqError.set('Please select a question type.'); return; }
    if (!this.aqText().trim()) { this.aqError.set('Question text is required.'); return; }

    const sectionId = this.aqSectionId()!;
    const attrs     = this.buildAttributes();
    const editId    = this.aqEditingQuestionId();

    this.aqSaving.set(true);
    this.aqError.set('');

    if (editId !== null) {
      this.formService.updateQuestion(
        this.form()!.formId, sectionId, editId,
        this.aqTypeId(), this.aqText().trim(), this.aqOrder(), attrs
      ).subscribe({
        next: () => {
          const typeName = this.questionTypes().find(t => t.questionTypeId === this.aqTypeId())?.questionTypeName ?? '';
          const updated = { ...this.form()! };
          const sec = updated.sections.find(s => s.sectionId === sectionId)!;
          sec.questions = sec.questions.map(q =>
            q.questionId === editId
              ? { ...q, text: this.aqText().trim(), questionTypeId: this.aqTypeId(), questionTypeName: typeName, order: this.aqOrder(), questionAttributes: attrs }
              : q
          );
          this.form.set(updated);
          this.aqEditingQuestionId.set(null);
          this.resetAqFields();
          this.aqSaving.set(false);
        },
        error: () => {
          this.aqError.set('Failed to update question.');
          this.aqSaving.set(false);
        }
      });
    } else {
      this.formService.addQuestion(
        this.form()!.formId,
        sectionId,
        this.aqTypeId(),
        this.aqText().trim(),
        this.aqOrder(),
        attrs
      ).subscribe({
        next: ({ questionId, order }) => {
          const typeName = this.questionTypes().find(t => t.questionTypeId === this.aqTypeId())?.questionTypeName ?? '';
          const newQ: QuestionDetail = {
            questionId,
            order,
            text: this.aqText().trim(),
            questionTypeId: this.aqTypeId(),
            questionTypeName: typeName,
            questionAttributes: attrs
          };

          const updated = { ...this.form()! };
          const sec = updated.sections.find(s => s.sectionId === sectionId)!;
          sec.questions = [...sec.questions, newQ];
          this.form.set(updated);

          this.aqOrder.set(sec.questions.length + 1);
          this.resetAqFields();
          this.aqSaving.set(false);
        },
        error: () => {
          this.aqError.set('Failed to save question. Please try again.');
          this.aqSaving.set(false);
        }
      });
    }
  }

  removeQuestion(section: SectionDetail, questionId: number): void {
    this.formService.removeQuestion(this.form()!.formId, section.sectionId, questionId)
      .subscribe({
        next: () => {
          if (this.aqEditingQuestionId() === questionId) {
            this.aqEditingQuestionId.set(null);
            this.resetAqFields();
          }
          const updated = { ...this.form()! };
          const sec = updated.sections.find(s => s.sectionId === section.sectionId)!;
          sec.questions = sec.questions.filter(q => q.questionId !== questionId);
          this.form.set(updated);
        }
      });
  }

  // ── Add Section ───────────────────────────────────────────────────────────

  openAddSection(): void {
    this.asName.set('');
    this.asOrder.set((this.form()?.sections.length ?? 0) + 1);
    this.asError.set('');
    this.asOpen.set(true);
  }

  cancelAddSection(): void {
    this.asOpen.set(false);
  }

  saveSection(): void {
    if (this.asSaving()) return;
    this.asSaving.set(true);
    this.asError.set('');

    this.formService.addSection(this.form()!.formId, this.asName(), this.asOrder())
      .subscribe({
        next: newSection => {
          const updated = { ...this.form()! };
          const section: SectionDetail = { ...newSection, questions: [] };
          updated.sections = [...updated.sections, section];
          this.form.set(updated);
          this.asOpen.set(false);
          this.asSaving.set(false);
          this.openAddPanel(section);
        },
        error: () => {
          this.asError.set('Failed to create section.');
          this.asSaving.set(false);
        }
      });
  }

  // ── Form Settings ─────────────────────────────────────────────────────────

  saveSettings(): void {
    this.fsSaving.set(true);
    this.fsSaved.set(false);
    this.fsError.set('');

    const f = this.form();
    if (f) {
      const hasFileQ = f.sections
        .flatMap(s => s.questions)
        .some(q => q.questionTypeId === 10 || q.questionTypeId === 11);
      if (hasFileQ && this.fsSecurityTypeId() !== 2) {
        this.fsError.set('Forms with image or PDF questions must use Private access.');
        this.fsSaving.set(false);
        return;
      }
      if (hasFileQ) {
        const q = parseInt(this.fsQuota(), 10);
        if (isNaN(q) || q < 1 || q > 25) {
          this.fsError.set('Forms with image or PDF questions require a quota between 1 and 25.');
          this.fsSaving.set(false);
          return;
        }
      }
    }

    const quota = parseInt(this.fsQuota(), 10);

    this.formService.patchSettings(
      this.form()!.formId,
      this.fsFormName(),
      this.fsDescription() || undefined,
      this.fsRandomize(),
      isNaN(quota) || quota <= 0 ? null : quota,
      this.fsPublished(),
      this.fsSecurityTypeId()
    ).subscribe({
      next: () => {
        const f = this.form()!;
        this.form.set({
          ...f,
          formName:       this.fsFormName(),
          description:    this.fsDescription() || undefined,
          published:      this.fsPublished(),
          randomizeOrder: this.fsRandomize(),
          securityTypeId: this.fsSecurityTypeId(),
        });
        this.fsSaving.set(false);
        this.fsSaved.set(true);
      },
      error: () => { this.fsSaving.set(false); }
    });
  }

  // ── Delete Form ───────────────────────────────────────────────────────────

  deleteForm(): void {
    this.deleting.set(true);
    this.formService.deleteForm(this.form()!.formId).subscribe({
      next: () => this.router.navigate(['/my-forms']),
      error: () => { this.deleting.set(false); this.showDeleteConfirm.set(false); }
    });
  }
}
