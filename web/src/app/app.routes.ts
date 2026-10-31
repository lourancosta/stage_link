import { Routes } from '@angular/router';
import { SpeakerList } from './speakers/speaker-list';

export const routes: Routes = [
  { path: '', component: SpeakerList, title: 'Speakers · StageLink' },
  {
    path: 'speakers/:id',
    // Lazy loading: this page's code is only downloaded when someone opens it.
    loadComponent: () => import('./speakers/speaker-page').then((m) => m.SpeakerPage),
    title: 'Speaker · StageLink',
  },
  { path: '**', redirectTo: '' },
];
