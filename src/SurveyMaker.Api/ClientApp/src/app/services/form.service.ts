import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  FormDetail, FormSummary, QuestionType, SectionDetail,
  SubmissionsResponse, SubmissionDetail, AnswerGrid
} from '../models/form.model';

@Injectable({ providedIn: 'root' })
export class FormService {
  private readonly http = inject(HttpClient);

  listForms(): Observable<FormSummary[]> {
    return this.http.get<FormSummary[]>('/api/forms');
  }

  create(formName: string, description: string | undefined, securityTypeId: number): Observable<{ formId: string }> {
    return this.http.post<{ formId: string }>('/api/forms', { formName, description, securityTypeId });
  }

  getDetail(formId: string): Observable<FormDetail> {
    return this.http.get<FormDetail>(`/api/forms/${formId}`);
  }

  patchSettings(
    formId: string,
    formName: string,
    description: string | undefined,
    randomizeOrder: boolean,
    quota: number | null,
    published: boolean
  ): Observable<void> {
    return this.http.patch<void>(`/api/forms/${formId}`, {
      formName, description, randomizeOrder, quota, published
    });
  }

  updateQuestion(
    formId: string,
    sectionId: number,
    questionId: number,
    questionTypeId: number,
    text: string,
    order: number,
    questionAttributes: string
  ): Observable<void> {
    return this.http.put<void>(
      `/api/forms/${formId}/sections/${sectionId}/questions/${questionId}`,
      { questionTypeId, text, order, questionAttributes }
    );
  }

  deleteForm(formId: string): Observable<void> {
    return this.http.delete<void>(`/api/forms/${formId}`);
  }

  addSection(formId: string, sectionName: string, order: number): Observable<SectionDetail> {
    return this.http.post<SectionDetail>(`/api/forms/${formId}/sections`, { sectionName, order });
  }

  addQuestion(
    formId: string,
    sectionId: number,
    questionTypeId: number,
    text: string,
    order: number,
    questionAttributes: string | null
  ): Observable<{ questionId: number; order: number }> {
    return this.http.post<{ questionId: number; order: number }>(
      `/api/forms/${formId}/sections/${sectionId}/questions`,
      { questionTypeId, text, order, questionAttributes }
    );
  }

  removeQuestion(formId: string, sectionId: number, questionId: number): Observable<void> {
    return this.http.delete<void>(
      `/api/forms/${formId}/sections/${sectionId}/questions/${questionId}`
    );
  }

  getQuestionTypes(): Observable<QuestionType[]> {
    return this.http.get<QuestionType[]>('/api/question-types');
  }

  listSubmissions(formId: string): Observable<SubmissionsResponse> {
    return this.http.get<SubmissionsResponse>(`/api/forms/${formId}/submissions`);
  }

  getSubmission(formId: string, submissionId: string): Observable<SubmissionDetail> {
    return this.http.get<SubmissionDetail>(`/api/forms/${formId}/submissions/${submissionId}`);
  }

  getAnswerGrid(formId: string): Observable<AnswerGrid> {
    return this.http.get<AnswerGrid>(`/api/forms/${formId}/grid`);
  }
}
