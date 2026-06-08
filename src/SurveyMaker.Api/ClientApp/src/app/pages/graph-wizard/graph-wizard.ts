import { Component, OnInit, computed, signal, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SurveyChart } from '../../components/survey-chart/survey-chart';
import { QuestionDetail } from '../../models/form.model';
import { GraphType } from '../../models/survey.model';

interface GraphTypeMeta { value: GraphType; label: string; icon: string; }

const GRAPH_TYPES: GraphTypeMeta[] = [
  { value: 'bar',       label: 'Bar',       icon: '▊' },
  { value: 'line',      label: 'Line',      icon: '📈' },
  { value: 'histogram', label: 'Histogram', icon: '▉' },
  { value: 'pie',       label: 'Pie',       icon: '◔' },
  { value: 'radar',     label: 'Radar',     icon: '✦' },
];

const SAMPLES = [65, 45, 78, 52, 88, 60, 72, 38];

@Component({
  selector: 'app-graph-wizard',
  imports: [FormsModule, SurveyChart],
  templateUrl: './graph-wizard.html',
  styleUrl: './graph-wizard.scss'
})
export class GraphWizard implements OnInit {
  questions    = input.required<QuestionDetail[]>();
  initialType  = input<GraphType>('bar');
  initialIds   = input<number[]>([]);
  saved        = output<{ graphType: GraphType; sourceQuestionIds: number[] }>();
  cancelled    = output<void>();

  readonly graphTypes = GRAPH_TYPES;

  selectedType = signal<GraphType>('bar');
  selectedIds  = signal<number[]>([]);

  previewLabels = computed(() =>
    this.selectedIds().map(id => {
      const q = this.questions().find(q => q.questionId === id);
      const txt = q?.text ?? `Q${id}`;
      return txt.length > 20 ? txt.slice(0, 20) + '…' : txt;
    })
  );

  previewValues = computed(() =>
    this.selectedIds().map((_, i) => SAMPLES[i % SAMPLES.length])
  );

  previewReady = computed(() => this.selectedIds().length >= 2);

  canSave = computed(() => this.selectedIds().length >= 2);

  ngOnInit(): void {
    this.selectedType.set(this.initialType());
    this.selectedIds.set([...this.initialIds()]);
  }

  isSelected(id: number): boolean {
    return this.selectedIds().includes(id);
  }

  toggleSource(id: number): void {
    this.selectedIds.update(ids =>
      ids.includes(id) ? ids.filter(x => x !== id) : [...ids, id]
    );
  }

  save(): void {
    if (!this.canSave()) return;
    this.saved.emit({ graphType: this.selectedType(), sourceQuestionIds: this.selectedIds() });
  }

  cancel(): void { this.cancelled.emit(); }
}
