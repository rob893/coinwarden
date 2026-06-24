import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Spinner } from '@/components/ui/spinner';
import { FormField } from '../components/FormField';
import { authApi } from '../services/auth';

export function ForgotPasswordPage() {
  const [email, setEmail] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [isSubmitted, setIsSubmitted] = useState(false);
  const navigate = useNavigate();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);

    try {
      await authApi.forgotPassword({ email });
    } catch {
      // Always show success per security best practices (avoid account enumeration)
    } finally {
      setIsLoading(false);
      setIsSubmitted(true);
    }
  };

  if (isSubmitted) {
    return (
      <div className="min-h-screen bg-linear-to-br from-background to-content1 flex items-center justify-center p-4">
        <Card className="w-full max-w-md shadow-2xl">
          <CardHeader className="flex flex-col items-center pb-6 pt-8">
            <h1 className="text-3xl font-bold text-primary mb-4">Coinwarden</h1>
            <Badge variant="success">✅ Email Sent</Badge>
          </CardHeader>

          <CardContent className="px-8 pb-8 text-center">
            <h2 className="text-2xl font-bold text-foreground mb-4">Check Your Email</h2>
            <p className="text-default-600 mb-6 leading-relaxed">
              If an account with that email exists, we've sent a password reset link. Please check your inbox.
            </p>
            <p className="text-sm text-default-500 mb-8">Didn't receive an email? Check your spam folder or try again.</p>

            <div className="space-y-3">
              <Button className="w-full" onClick={() => navigate('/login')}>Back to Sign In</Button>
              <Button variant="ghost" className="w-full" onClick={() => { setIsSubmitted(false); setEmail(''); }}>
                Try Different Email
              </Button>
            </div>
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
          <h2 className="text-2xl font-semibold text-foreground mb-2">Forgot Password</h2>
          <p className="text-default-600 text-center">
            Enter your email and we'll send you a link to reset your password.
          </p>
        </CardHeader>

        <CardContent className="px-8 pb-8">
          <form onSubmit={handleSubmit} className="space-y-6">
            <FormField
              label="Email Address"
              type="email"
              value={email}
              onChange={setEmail}
              isRequired
              isDisabled={isLoading}
              placeholder="Enter your email address"
              autoComplete="email"
              description="We'll send a password reset link to this address"
            />

            <Button type="submit" className="w-full font-semibold" disabled={isLoading || !email}>
              {isLoading && <Spinner color="current" size="sm" className="mr-2" />}
              {isLoading ? 'Sending Reset Link...' : 'Send Reset Link'}
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
