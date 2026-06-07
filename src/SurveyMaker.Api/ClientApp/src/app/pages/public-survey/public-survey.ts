import { Component, OnInit, signal, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SlicePipe } from '@angular/common';
import { SurveyService } from '../../services/survey.service';
import { SurveySummary } from '../../models/survey.model';

@Component({
  selector: 'app-public-survey',
  imports: [RouterLink, SlicePipe],
  templateUrl: './public-survey.html',
  styleUrl: './public-survey.scss'
})
export class PublicSurvey implements OnInit {
  private readonly surveyService = inject(SurveyService);

  surveys = signal<SurveySummary[]>([]);
  loading = signal(true);
  error   = signal('');

  ngOnInit(): void {
    this.surveyService.listPublic().subscribe({
      next:  s  => { this.surveys.set(s); this.loading.set(false); },
      error: () => { this.error.set('Could not load surveys.'); this.loading.set(false); }
    });
  }
}
