import { Component, OnDestroy, effect, viewChild, ElementRef, input } from '@angular/core';
import { Chart, ChartType, ChartData, ChartOptions, registerables } from 'chart.js';
import { GraphType } from '../../models/survey.model';

Chart.register(...registerables);

const PALETTE = [
  '#0096c7', '#00b4d8', '#48cae4', '#023047',
  '#f77f00', '#fcbf49', '#90e0ef', '#ade8f4'
];

@Component({
  selector: 'app-survey-chart',
  standalone: true,
  template: `<div class="chart-host"><canvas #canvas></canvas></div>`,
  styleUrl: './survey-chart.scss'
})
export class SurveyChart implements OnDestroy {
  readonly graphType = input.required<GraphType>();
  readonly labels    = input.required<string[]>();
  readonly values    = input.required<number[]>();

  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  private chart: Chart | null = null;

  constructor() {
    effect(() => {
      this.render(this.graphType(), this.labels(), this.values());
    });
  }

  ngOnDestroy(): void {
    this.chart?.destroy();
    this.chart = null;
  }

  private render(type: GraphType, labels: string[], values: number[]): void {
    const el    = this.canvas().nativeElement;
    const jsType = (type === 'histogram' ? 'bar' : type) as ChartType;

    if (this.chart && this.chart.config.type !== jsType) {
      this.chart.destroy();
      this.chart = null;
    }

    if (!this.chart) {
      this.chart = new Chart(el, {
        type: jsType,
        data: this.buildData(type, labels, values),
        options: this.buildOptions(type)
      });
    } else {
      this.chart.data = this.buildData(type, labels, values);
      this.chart.update('none');
    }
  }

  private buildData(type: GraphType, labels: string[], values: number[]): ChartData {
    if (type === 'pie') {
      return {
        labels,
        datasets: [{ data: values, backgroundColor: PALETTE, borderWidth: 1 }]
      };
    }
    if (type === 'radar') {
      return {
        labels,
        datasets: [{
          data: values,
          backgroundColor: 'rgba(0,150,199,0.15)',
          borderColor: '#0096c7',
          pointBackgroundColor: '#0096c7',
          pointRadius: 4,
          fill: true
        }]
      };
    }
    return {
      labels,
      datasets: [{
        label: 'Value',
        data: values,
        backgroundColor: type === 'line' ? 'rgba(0,150,199,0.08)' : PALETTE,
        borderColor: '#0096c7',
        borderWidth: 2,
        fill: type === 'line',
        tension: type === 'line' ? 0.35 : 0,
        pointRadius: type === 'line' ? 4 : 0
      }]
    };
  }

  private buildOptions(type: GraphType): ChartOptions {
    const base: ChartOptions = {
      responsive: true,
      maintainAspectRatio: true,
      animation: false,
      plugins: { legend: { display: type === 'pie' } }
    };
    if (type === 'pie' || type === 'radar') return base;
    return {
      ...base,
      scales: { y: { beginAtZero: true } }
    };
  }
}
