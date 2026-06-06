import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-create-form',
  imports: [FormsModule, RouterLink],
  templateUrl: './create-form.html',
  styleUrl: './create-form.scss'
})
export class CreateForm {
  title = signal('');
  description = signal('');
}
