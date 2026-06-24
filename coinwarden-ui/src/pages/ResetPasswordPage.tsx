import React, { useState, useEffect } from 'react';
import { useNavigate, useSearchParams, Link } from 'react-router';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Spinner } from '@/components/ui/spinner';
import { ApiErrorDisplay } from '../components/ApiErrorDisplay';
import { showErrorDetails } from '../utils/environment';
import { FormField } from '../components/FormField';
import { authApi } from '../services/auth';
import {
  validatePassword,
  getPasswordRequirementsDescription,
  type PasswordValidationResult
} from '../utils/passwordValidation';

export function ResetPasswordPage() {
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState<Error | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isSubmitted, setIsSubmitted] = useState(false);
  const [passwordValidation, setPasswordValidation] = useState<PasswordValidationResult>({ isValid: false, errors: [] });

  const [searchParams] = useSearchParams();
  const navigate = useNavigate();

  const token = searchParams.get('token');
  const email = searchParams.get('email');

  useEffect(() => {
    if (password) setPasswordValidation(validatePassword(password));
  }, [password]);

  useEffect(() => {
    if (!token || !email) navigate('/forgot-password', { replace: true });
  }, [token, email, navigate]);

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

    if (!token || !email) {
      setError(new Error('Invalid reset link'));
      return;
    }

    setIsLoading(true);

    try {
      await authApi.resetPassword({ email, token, password });

      setIsSubmitted(true);
    } catch {
      setError(new Error('Failed to reset password. The reset link may be invalid or expired.'));
    } finally {
      setIsLoading(false);
    }
  };

  if (isSubmitted) {
    return (
      <div className="min-h-screen bg-linear-to-br from-background to-content1 flex items-center justify-center p-4">
        <Card className="w-full max-w-md shadow-2xl">
          <CardHeader className="flex flex-col items-center pb-6 pt-8">
            <h1 className="text-3xl font-bold text-primary mb-4">Coinwarden</h1>
            <Badge variant="success">✅ Password Reset</Badge>
          </CardHeader>
          <CardContent className="px-8 pb-8 text-center">
            <h2 className="text-2xl font-bold text-foreground mb-4">Password Updated!</h2>
            <p className="text-default-600 mb-8 leading-relaxed">
              Your password has been updated. You can now sign in with your new password.
            </p>
            <Button className="w-full" onClick={() => navigate('/login')}>Sign In</Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (!token || !email) {
    return (
      <div className="min-h-screen bg-linear-to-br from-background to-content1 flex items-center justify-center p-4">
        <Card className="w-full max-w-md shadow-2xl">
          <CardHeader className="flex flex-col items-center pb-6 pt-8">
            <h1 className="text-3xl font-bold text-primary mb-4">Coinwarden</h1>
            <Badge variant="destructive">❌ Invalid Link</Badge>
          </CardHeader>
          <CardContent className="px-8 pb-8 text-center">
            <h2 className="text-2xl font-bold text-foreground mb-4">Link Invalid or Expired</h2>
            <p className="text-default-600 mb-8">Please request a new password reset link.</p>
            <Button className="w-full" onClick={() => navigate('/forgot-password')}>Request New Reset Link</Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-linear-to-br from-background to-content1 flex items-center justify-center p-4">
      <Card className="w-full max-w-md shadow-2xl">
        <CardHeader className="flex flex-col items-center pb-6 pt-8">
          <h1 className="text-3xl font-bold text-primary mb-4">Coinwarden</h1>
          <h2 className="text-2xl font-semibold text-foreground mb-2">Reset Password</h2>
          <p className="text-default-600 text-center">Enter your new password below.</p>
        </CardHeader>

        <CardContent className="px-8 pb-8">
          <form onSubmit={handleSubmit} className="space-y-4">
            {error && <ApiErrorDisplay error={error} title="Password Reset Failed" showDetails={showErrorDetails} />}

            <div className="mb-4 p-3 bg-default-50 rounded-lg border border-default-200">
              <p className="text-sm text-default-600">
                <strong>Resetting password for:</strong> {email}
              </p>
            </div>

            <FormField
              label="New Password"
              name="password"
              type="password"
              value={password}
              onChange={setPassword}
              isRequired
              isDisabled={isLoading}
              placeholder="Enter your new password"
              autoComplete="new-password"
              description={getPasswordRequirementsDescription()}
              isInvalid={password.length > 0 && !passwordValidation.isValid}
              errorMessage={password.length > 0 && !passwordValidation.isValid ? passwordValidation.errors.join(', ') : undefined}
            />

            <FormField
              label="Confirm New Password"
              name="confirmPassword"
              type="password"
              value={confirmPassword}
              onChange={setConfirmPassword}
              isRequired
              isDisabled={isLoading}
              placeholder="Confirm your new password"
              autoComplete="new-password"
              isInvalid={confirmPassword.length > 0 && password !== confirmPassword}
              errorMessage={confirmPassword.length > 0 && password !== confirmPassword ? 'Passwords do not match' : undefined}
            />

            <Button
              type="submit"
              className="w-full font-semibold mt-6"
              disabled={isLoading || !password || !confirmPassword || !passwordValidation.isValid || password !== confirmPassword}
            >
              {isLoading && <Spinner color="current" size="sm" className="mr-2" />}
              {isLoading ? 'Updating Password...' : 'Update Password'}
            </Button>
          </form>

          <div className="mt-6 text-center">
            <p className="text-default-600">
              Remember your password?{' '}
              <Link to="/login" className="text-primary hover:text-primary-600 font-medium transition-colors">
                Back to Sign In
              </Link>
            </p>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
