import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { SharedModule } from 'src/app/theme/shared/shared.module';

@Component({
  selector: 'app-auth-signin',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, SharedModule],
  templateUrl: './auth-signin.component.html',
  styleUrls: ['./auth-signin.component.scss']
})
export class AuthSigninComponent {
  email = '';
  password = '';
  submitted = signal(false);
  error = signal('');
  showPassword = signal(false);

  onSubmit(event: Event) {
    event.preventDefault();
    this.submitted.set(true);
    console.log('User signed in:', this.email);
  }

  togglePasswordVisibility() {
    this.showPassword.set(!this.showPassword());
  }
}
