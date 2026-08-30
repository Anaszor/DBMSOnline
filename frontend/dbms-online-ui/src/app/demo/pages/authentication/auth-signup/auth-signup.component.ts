import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { SharedModule } from 'src/app/theme/shared/shared.module';

@Component({
  selector: 'app-auth-signup',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, SharedModule],
  templateUrl: './auth-signup.component.html',
  styleUrls: ['./auth-signup.component.scss']
})
export class AuthSignupComponent {
  username = '';
  email = '';
  password = '';
  submitted = signal(false);
  error = signal('');
  showPassword = signal(false);

  onSubmit(event: Event) {
    event.preventDefault();
    this.submitted.set(true);
    console.log('User signed up:', this.username, this.email);
  }

  togglePasswordVisibility() {
    this.showPassword.set(!this.showPassword());
  }
}
