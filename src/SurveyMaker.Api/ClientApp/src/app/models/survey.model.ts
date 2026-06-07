export interface SurveySummary {
  formId: string;
  formName: string;
  description?: string;
  questionCount: number;
  createdAt: string;
}

export interface SurveyDetail {
  formId: string;
  formName: string;
  description?: string;
  sections: SurveySectionDetail[];
}

export interface SurveySectionDetail {
  sectionId: number;
  sectionName: string;
  order: number;
  questions: SurveyQuestion[];
}

export interface SurveyQuestion {
  questionId: number;
  order: number;
  text: string;
  questionTypeId: number;
  questionTypeName: string;
  questionAttributes?: string;
}

export interface ParsedAttrs {
  required?: boolean;
  min?: number;
  max?: number;
  options?: string[] | ScoredOption[];
  scale?: number;
}

export interface ScoredOption {
  text: string;
  value: number;
}

export interface MySurveyCompleted {
  submissionId: string;
  formId: string;
  formName: string;
  submittedAt: string | null;
}

export interface MySurveyInProgress {
  submissionId: string;
  formId: string;
  formName: string;
  startedAt: string;
}

export interface MySurveyAssigned {
  formId: string;
  formName: string;
  description?: string;
  questionCount: number;
}

export interface MySurveysData {
  completed: MySurveyCompleted[];
  inProgress: MySurveyInProgress[];
  assigned: MySurveyAssigned[];
}

export interface SurveyAnswerPayload {
  questionId: number;
  answerScalar?: string;
  answerJson?: string;
}

// Questions enriched with pre-parsed attrs — used internally in take-survey
export interface LoadedQuestion extends SurveyQuestion {
  attrs: ParsedAttrs;
}

export interface LoadedSection {
  sectionId: number;
  sectionName: string;
  order: number;
  questions: LoadedQuestion[];
}

export interface LoadedSurvey {
  formId: string;
  formName: string;
  description?: string;
  sections: LoadedSection[];
  allQuestions: LoadedQuestion[];
}
