import { Routes } from '@angular/router';
import { Home } from './home/home';
import { Layout } from './layout/layout';
import { authGuard } from './guards/auth.guard';
import { Dashboard } from './pages/dashboard/dashboard';
import { MyForms } from './pages/my-forms/my-forms';
import { CreateForm } from './pages/create-form/create-form';
import { MySurveys } from './pages/my-surveys/my-surveys';
import { PublicSurvey } from './pages/public-survey/public-survey';

export const routes: Routes = [
  { path: '', pathMatch: 'full', component: Home },
  {
    path: '',
    component: Layout,
    canActivate: [authGuard],
    children: [
      { path: 'dashboard',     component: Dashboard },
      { path: 'my-forms',      component: MyForms },
      { path: 'create-form',   component: CreateForm },
      { path: 'my-surveys',    component: MySurveys },
      { path: 'public-survey', component: PublicSurvey },
    ]
  },
  { path: '**', redirectTo: '' }
];
