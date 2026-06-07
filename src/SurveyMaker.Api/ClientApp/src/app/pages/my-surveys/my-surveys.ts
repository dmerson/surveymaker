import { Component, OnInit, signal, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SlicePipe } from '@angular/common';
import { SurveyService } from '../../services/survey.service';
import {
  MySurveyCompleted,
  MySurveyInProgress,
  MySurveyAssigned
} from '../../models/survey.model';

@Component({
  selector: 'app-my-surveys',
  imports: [RouterLink, SlicePipe],
  templateUrl: './my-surveys.html',
  styleUrl: './my-surveys.scss'
})
export class MySurveys implements OnInit {
  private readonly surveyService = inject(SurveyService);

  loading    = signal(true);
  error      = signal('');
  completed  = signal<MySurveyCompleted[]>([]);
  inProgress = signal<MySurveyInProgress[]>([]);
  assigned   = signal<MySurveyAssigned[]>([]);

  ngOnInit(): void {
    this.surveyService.getMySurveys().subscribe({
      next: data => {
        this.completed.set(data.completed);
        this.inProgress.set(data.inProgress);
        this.assigned.set(data.assigned);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load your surveys.');
        this.loading.set(false);
      }
    });
  }
}
