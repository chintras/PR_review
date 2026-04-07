import { Routes } from '@angular/router';
import { ReviewFormComponent } from './components/review-form/review-form.component';

export const routes: Routes = [
  { path: '', component: ReviewFormComponent },
  { path: '**', redirectTo: '' }
];
