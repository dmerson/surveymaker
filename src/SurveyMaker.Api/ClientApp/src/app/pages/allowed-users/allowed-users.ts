import { Component, OnInit, signal, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { FormService } from '../../services/form.service';

interface AllowedUser {
  formAllowedUserId: number;
  userEmail: string;
}

@Component({
  selector: 'app-allowed-users',
  imports: [RouterLink, FormsModule],
  templateUrl: './allowed-users.html',
  styleUrl: './allowed-users.scss'
})
export class AllowedUsers implements OnInit {
  private readonly route       = inject(ActivatedRoute);
  private readonly formService = inject(FormService);

  formId   = signal('');
  users    = signal<AllowedUser[]>([]);
  loading  = signal(true);
  error    = signal('');

  newEmail = signal('');
  adding   = signal(false);
  addError = signal('');

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.formId.set(id);
    this.load(id);
  }

  private load(formId: string): void {
    this.formService.listAllowedUsers(formId).subscribe({
      next:  users => { this.users.set(users); this.loading.set(false); },
      error: ()    => { this.error.set('Could not load allowed users.'); this.loading.set(false); }
    });
  }

  add(): void {
    const email = this.newEmail().trim().toLowerCase();
    if (!email) { this.addError.set('Please enter an email address.'); return; }
    this.adding.set(true);
    this.addError.set('');

    this.formService.addAllowedUser(this.formId(), email).subscribe({
      next: user => {
        this.users.update(list => [...list, user].sort((a, b) => a.userEmail.localeCompare(b.userEmail)));
        this.newEmail.set('');
        this.adding.set(false);
      },
      error: (err) => {
        this.addError.set(err?.error?.error ?? 'Failed to add user.');
        this.adding.set(false);
      }
    });
  }

  remove(user: AllowedUser): void {
    this.formService.removeAllowedUser(this.formId(), user.formAllowedUserId).subscribe({
      next: () => this.users.update(list => list.filter(u => u.formAllowedUserId !== user.formAllowedUserId))
    });
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter') this.add();
  }
}
