export interface PasswordValidationResult {
  isValid: boolean;
  errors: string[];
}

export function validatePassword(password: string): PasswordValidationResult {
  const errors: string[] = [];

  if (password.length < 8) {
    errors.push('Password must be at least 8 characters long');
  }

  if (!/\d/.test(password)) {
    errors.push('Password must contain at least 1 number');
  }

  if (!/[^a-zA-Z0-9]/.test(password)) {
    errors.push('Password must contain at least 1 special character');
  }

  return {
    isValid: errors.length === 0,
    errors
  };
}

export function getPasswordRequirementsDescription(): string {
  return '8+ characters, 1+ number, 1+ special character';
}
