export interface FormSummary {
  formId: string;
  formName: string;
  description?: string;
  securityTypeId: number;
  published: boolean;
  questionCount: number;
  createdAt: string;
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
