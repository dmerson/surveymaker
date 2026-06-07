import { Routes } from '@angular/router';
import { Home } from './home/home';
import { Layout } from './layout/layout';
import { authGuard } from './guards/auth.guard';
import { Dashboard } from './pages/dashboard/dashboard';
import { MyForms } from './pages/my-forms/my-forms';
import { CreateForm } from './pages/create-form/create-form';
import { MySurveys } from './pages/my-surveys/my-surveys';
import { PublicSurvey } from './pages/public-survey/public-survey';
import { FormEditor } from './pages/form-editor/form-editor';
import { TakeSurvey } from './pages/take-survey/take-survey';
import { FormAnswers } from './pages/form-answers/form-answers';
import { ViewSubmission } from './pages/view-submission/view-submission';
import { AllAnswers } from './pages/all-answers/all-answers';
import { AllowedUsers } from './pages/allowed-users/allowed-users';

export const routes: Routes = [
  { path: '', pathMatch: 'full', component: Home },
  { path: 'survey/:id', component: TakeSurvey },
  {
    path: '',
    component: Layout,
    canActivate: [authGuard],
    children: [
      { path: 'dashboard',                                   component: Dashboard },
      { path: 'my-forms',                                    component: MyForms },
      { path: 'create-form',                                 component: CreateForm },
      { path: 'forms/:id/edit',                              component: FormEditor },
      { path: 'forms/:id/answers',                           component: FormAnswers },
      { path: 'forms/:id/answers/:submissionId',             component: ViewSubmission },
      { path: 'forms/:id/all-answers',                       component: AllAnswers },
      { path: 'forms/:id/allowed-users',                     component: AllowedUsers },
      { path: 'my-surveys',                                  component: MySurveys },
      { path: 'public-survey',                               component: PublicSurvey },
    ]
  },
  { path: '**', redirectTo: '' }
];
