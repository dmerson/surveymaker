import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { MySurveysData, ProgressResponse, SurveyAnswerPayload, SurveyDetail, SurveySummary } from '../models/survey.model';

@Injectable({ providedIn: 'root' })
export class SurveyService {
  private readonly http = inject(HttpClient);

  listPublic(): Observable<SurveySummary[]> {
    return this.http.get<SurveySummary[]>('/api/surveys');
  }

  getSurvey(formId: string): Observable<SurveyDetail> {
    return this.http.get<SurveyDetail>(`/api/surveys/${formId}`);
  }

  getMySurveys(): Observable<MySurveysData> {
    return this.http.get<MySurveysData>('/api/surveys/mine');
  }

  loadProgress(formId: string): Observable<ProgressResponse> {
    return this.http.get<ProgressResponse>(`/api/surveys/${formId}/progress`);
  }

  saveProgress(formId: string, answers: SurveyAnswerPayload[]): Observable<{ submissionId: string }> {
    return this.http.post<{ submissionId: string }>(
      `/api/surveys/${formId}/progress`,
      { answers }
    );
  }

  submit(formId: string, answers: SurveyAnswerPayload[]): Observable<{ submissionId: string }> {
    return this.http.post<{ submissionId: string }>(
      `/api/surveys/${formId}/submit`,
      { answers }
    );
  }
}
