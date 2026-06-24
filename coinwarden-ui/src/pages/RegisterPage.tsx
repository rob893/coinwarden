import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Separator } from '@/components/ui/separator';
import { Spinner } from '@/components/ui/spinner';
import { ApiErrorDisplay } from '../components/ApiErrorDisplay';
import { showErrorDetails } from '../utils/environment';
import { FormField } from '../components/FormField';
import { SocialLoginButtons } from '../components/SocialLoginButtons';
import { useAuth } from '../hooks/useAuth';
import {
  validatePassword,
  getPasswordRequirementsDescription,
  type PasswordValidationResult
} from '../utils/passwordValidation';

export function RegisterPage() {
  const [userName, setUserName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState<Error | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [passwordValidation, setPasswordValidation] = useState<PasswordValidationResult>({ isValid: false, errors: [] });

  const { register } = useAuth();
  const navigate = useNavigate();

  const handlePasswordChange = (value: string) => {
    setPassword(value);
    setPasswordValidation(validatePassword(value));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    const result = validatePassword(password);
    if (!result.isValid) {
      setError(new Error(result.errors.join(', ')));
      return;
    }

    if (password !== confirmPassword) {
      setError(new Error('Passwords do not match'));
      return;
    }

    setIsLoading(true);

    try {
      await register({ userName, email, password });
      navigate('/', { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err : new Error('Registration failed'));
    } finally {
      setIsLoading(false);
    }
  };

  const isDisabled =
    !userName || !email || !password || !confirmPassword || !passwordValidation.isValid || password !== confirmPassword;

  return (
    <div className="min-h-screen bg-linear-to-br from-background to-content1 flex items-center justify-center p-4">
      <Card className="w-full max-w-md shadow-2xl">
        <CardHeader className="flex flex-col items-center pb-6 pt-8">
          <h1 className="text-4xl font-bold text-primary mb-2">Coinwarden</h1>
          <h2 className="text-2xl font-semibold text-foreground mb-2">Create Account</h2>
          <p className="text-default-600 text-center">Join Coinwarden today!</p>
        </CardHeader>

        <CardContent className="px-8 pb-8">
          <form onSubmit={handleSubmit} className="space-y-4">
            {error && <ApiErrorDisplay error={error} title="Registration Failed" showDetails={showErrorDetails} />}

            <FormField
              label="Username"
              name="userName"
              value={userName}
              onChange={setUserName}
              isRequired
              isDisabled={isLoading}
              placeholder="Choose a username"
              autoComplete="username"
            />

            <FormField
              label="Email"
              name="email"
              type="email"
              value={email}
              onChange={setEmail}
              isRequired
              isDisabled={isLoading}
              placeholder="Enter your email"
              autoComplete="email"
            />

            <FormField
              label="Password"
              name="password"
              type="password"
              value={password}
              onChange={handlePasswordChange}
              isRequired
              isDisabled={isLoading}
              placeholder="Choose a password"
              autoComplete="new-password"
              description={getPasswordRequirementsDescription()}
              isInvalid={password.length > 0 && !passwordValidation.isValid}
              errorMessage={password.length > 0 && !passwordValidation.isValid ? passwordValidation.errors.join(', ') : undefined}
            />

            <FormField
              label="Confirm Password"
              name="confirmPassword"
              type="password"
              value={confirmPassword}
              onChange={setConfirmPassword}
              isRequired
              isDisabled={isLoading}
              placeholder="Confirm your password"
              autoComplete="new-password"
              isInvalid={confirmPassword.length > 0 && password !== confirmPassword}
              errorMessage={confirmPassword.length > 0 && password !== confirmPassword ? 'Passwords do not match' : undefined}
            />

            <Button type="submit" className="w-full font-semibold" disabled={isLoading || isDisabled}>
              {isLoading && <Spinner color="current" size="sm" className="mr-2" />}
              {isLoading ? 'Creating Account...' : 'Create Account'}
            </Button>
          </form>

          <Separator className="my-6" />

          <SocialLoginButtons isDisabled={isLoading} onError={setError} />

          <Separator className="my-6" />

          <div className="text-center">
            <p className="text-default-600">
              Already have an account?{' '}
              <Link to="/login" className="text-primary hover:text-primary-600 font-medium transition-colors">
                Sign in
              </Link>
            </p>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
