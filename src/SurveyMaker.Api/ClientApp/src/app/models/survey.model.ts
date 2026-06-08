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
  isMatrix: boolean;
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

export type GraphType = 'bar' | 'line' | 'histogram' | 'pie' | 'radar';

export interface ParsedAttrs {
  required?: boolean;
  min?: number;
  max?: number;
  options?: string[] | ScoredOption[];
  scale?: number;
  tokens?: FormulaToken[];
  graphType?: GraphType;
  sourceQuestionIds?: number[];
  html?: string;
  help?: string;
  yesNoStyle?: 'radio' | 'checkbox';
  conditionalLogic?: ConditionalLogicConfig;
}

export type ConditionOperator = 'eq' | 'neq' | 'gt' | 'lt' | 'gte' | 'lte';
export type ThenActionType = 'show' | 'hide' | 'require';

export interface ConditionRule {
  questionId: number;
  operator: ConditionOperator;
  value: string;
}

export interface ThenAction {
  questionId: number;
  action: ThenActionType;
}

export interface ConditionalLogicConfig {
  condition: ConditionRule;
  thenActions: ThenAction[];
}

export type FormulaToken =
  | { type: 'question'; id: number; label: string }
  | { type: 'op'; value: '+' | '-' | '*' | '/' | 'mod' | ',' }
  | { type: 'fn'; value: 'sqrt' | 'abs' | 'mean' | 'max' | 'min' }
  | { type: 'paren'; value: '(' | ')' }
  | { type: 'number'; value: number };

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

export interface ProgressResponse {
  submissionId: string | null;
  answers: SurveyAnswerPayload[];
}

// Questions enriched with pre-parsed attrs — used internally in take-survey
export interface LoadedQuestion extends SurveyQuestion {
  attrs: ParsedAttrs;
}

export interface LoadedSection {
  sectionId: number;
  sectionName: string;
  order: number;
  isMatrix: boolean;
  questions: LoadedQuestion[];
}

export interface LoadedSurvey {
  formId: string;
  formName: string;
  description?: string;
  sections: LoadedSection[];
  allQuestions: LoadedQuestion[];
}
