export interface FormSummary {
  formId: string;
  formName: string;
  description?: string;
  securityTypeId: number;
  published: boolean;
  questionCount: number;
  responseCount: number;
  createdAt: string;
}

export interface SubmissionsResponse {
  formId: string;
  formName: string;
  submissions: SubmissionSummary[];
}

export interface SubmissionSummary {
  submissionId: string;
  userEmail: string | null;
  submittedAt: string | null;
  isComplete: boolean;
}

export interface SubmissionDetail {
  submissionId: string;
  formId: string;
  formName: string;
  description?: string;
  userEmail: string | null;
  submittedAt: string | null;
  sections: SubmissionSectionDetail[];
}

export interface SubmissionSectionDetail {
  sectionId: number;
  sectionName: string;
  order: number;
  questions: SubmissionQuestionDetail[];
}

export interface SubmissionQuestionDetail {
  questionId: number;
  order: number;
  text: string;
  questionTypeId: number;
  questionTypeName: string;
  questionAttributes?: string;
  answerScalar?: string;
  answerJson?: string;
}

export interface DashboardFormSummary {
  formId: string;
  formName: string;
  securityTypeId: number;
  published: boolean;
  createdAt: string;
}

export interface DashboardActivity {
  submissionId: string;
  formId: string;
  formName: string;
  userEmail: string | null;
  submittedAt: string | null;
  isComplete: boolean;
}

export interface DashboardData {
  myFormsCount: number;
  responsesReceived: number;
  surveysCompleted: number;
  recentForms: DashboardFormSummary[];
  recentActivity: DashboardActivity[];
}

export interface AnswerGridQuestion {
  questionId: number;
  text: string;
  questionTypeId: number;
}

export interface AnswerGridRow {
  submissionId: string;
  userEmail: string | null;
  submittedAt: string | null;
  cells: (string | null)[];
}

export interface AnswerGrid {
  formId: string;
  formName: string;
  questions: AnswerGridQuestion[];
  rows: AnswerGridRow[];
}

export interface FormDetail {
  formId: string;
  formName: string;
  description?: string;
  securityTypeId: number;
  randomizeOrder: boolean;
  quota?: number;
  published: boolean;
  sections: SectionDetail[];
}

export interface SectionDetail {
  sectionId: number;
  sectionName: string;
  order: number;
  isMatrix: boolean;
  questions: QuestionDetail[];
}

export interface QuestionDetail {
  questionId: number;
  order: number;
  text: string;
  questionTypeId: number;
  questionTypeName: string;
  questionAttributes?: string;
}

export interface QuestionType {
  questionTypeId: number;
  questionTypeName: string;
}
