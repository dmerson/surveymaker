import { Component, OnInit, computed, signal, inject } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { FormService } from '../../services/form.service';
import { FormDetail, QuestionDetail, QuestionType, SectionDetail } from '../../models/form.model';

@Component({
  selector: 'app-form-editor',
  imports: [FormsModule, RouterLink],
  templateUrl: './form-editor.html',
  styleUrl: './form-editor.scss'
})
export class FormEditor implements OnInit {
  // ── Data ─────────────────────────────────────────────────────────────────
  form          = signal<FormDetail | null>(null);
  questionTypes = signal<QuestionType[]>([]);
  loading       = signal(true);
  loadError     = signal('');

  // ── Add Question state ────────────────────────────────────────────────────
  aqSectionId  = signal<number | null>(null);  // null = panel closed
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
  // scored option types
  aqScored     = signal<{ text: string; value: string }[]>([]);
  aqPendText   = signal('');
  aqPendVal    = signal('');
  // scale types (Likert)
  aqScale      = signal(5);
  // status
  aqSaving     = signal(false);
  aqError      = signal('');

  // ── Add Section state ─────────────────────────────────────────────────────
  asOpen   = signal(false);
  asName   = signal('');
  asOrder  = signal(1);
  asSaving = signal(false);
  asError  = signal('');

  // ── Form settings ─────────────────────────────────────────────────────────
  fsRandomize    = signal(false);
  fsQuota        = signal('');
  fsSaving       = signal(false);
  fsSaved        = signal(false);

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
    };
  });

  private readonly route       = inject(ActivatedRoute);
  private readonly router      = inject(Router);
  private readonly formService = inject(FormService);

  ngOnInit(): void {
    const formId = this.route.snapshot.paramMap.get('id')!;

    this.formService.getDetail(formId).subscribe({
      next: detail => {
        this.form.set(detail);
        this.fsRandomize.set(detail.randomizeOrder);
        this.fsQuota.set(detail.quota ? String(detail.quota) : '');
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

  // ── Add Question ──────────────────────────────────────────────────────────

  openAddPanel(section: SectionDetail): void {
    this.aqSectionId.set(section.sectionId);
    this.aqOrder.set(section.questions.length + 1);
    this.resetAqFields();
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
    this.aqError.set('');
  }

  onTypeChange(typeId: number): void {
    this.aqTypeId.set(+typeId);
    this.aqMinLen.set(''); this.aqMaxLen.set('');
    this.aqMinVal.set(''); this.aqMaxVal.set('');
    this.aqOptions.set([]); this.aqPendOpt.set('');
    this.aqScored.set([]); this.aqPendText.set(''); this.aqPendVal.set('');
    this.aqScale.set(5);
    this.aqError.set('');
  }

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
    const val  = this.aqPendVal().trim();
    if (!text || !val) return;
    this.aqScored.update(opts => [...opts, { text, value: val }]);
    this.aqPendText.set(''); this.aqPendVal.set('');
  }

  removeScoredOption(i: number): void {
    this.aqScored.update(opts => opts.filter((_, idx) => idx !== i));
  }

  private buildAttributes(): string | null {
    const f = this.typeFlags();
    const attrs: Record<string, unknown> = {};

    if (this.aqRequired()) attrs['required'] = true;

    if (f.hasMinMax) {
      if (this.aqMinLen()) attrs['min'] = parseInt(this.aqMinLen(), 10);
      if (this.aqMaxLen()) attrs['max'] = parseInt(this.aqMaxLen(), 10);
    }
    if (f.hasNumberRange) {
      if (this.aqMinVal()) attrs['min'] = parseFloat(this.aqMinVal());
      if (this.aqMaxVal()) attrs['max'] = parseFloat(this.aqMaxVal());
    }
    if (f.hasOptions && this.aqOptions().length > 0) {
      attrs['options'] = this.aqOptions();
    }
    if (f.hasScored && this.aqScored().length > 0) {
      attrs['options'] = this.aqScored().map(o => ({ text: o.text, value: parseFloat(o.value) }));
    }
    if (f.hasScale) {
      attrs['scale'] = this.aqScale();
    }

    return Object.keys(attrs).length > 0 ? JSON.stringify(attrs) : null;
  }

  saveQuestion(): void {
    if (!this.aqTypeId()) { this.aqError.set('Please select a question type.'); return; }
    if (!this.aqText().trim()) { this.aqError.set('Question text is required.'); return; }

    const sectionId = this.aqSectionId()!;
    const section   = this.form()!.sections.find(s => s.sectionId === sectionId)!;
    const attrs     = this.buildAttributes();

    this.aqSaving.set(true);
    this.aqError.set('');

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
          questionAttributes: attrs ?? undefined
        };

        const updated = { ...this.form()! };
        const sec = updated.sections.find(s => s.sectionId === sectionId)!;
        sec.questions = [...sec.questions, newQ];
        this.form.set(updated);

        // Reset panel for next question
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

  removeQuestion(section: SectionDetail, questionId: number): void {
    this.formService.removeQuestion(this.form()!.formId, section.sectionId, questionId)
      .subscribe({
        next: () => {
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
    const quota = parseInt(this.fsQuota(), 10);

    this.formService.patchSettings(
      this.form()!.formId,
      this.fsRandomize(),
      isNaN(quota) || quota <= 0 ? null : quota
    ).subscribe({
      next: () => { this.fsSaving.set(false); this.fsSaved.set(true); },
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
