import React, { useState } from 'react';
import { Link, useNavigate, useLocation } from 'react-router';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Separator } from '@/components/ui/separator';
import { Spinner } from '@/components/ui/spinner';
import { ApiErrorDisplay } from '../components/ApiErrorDisplay';
import { showErrorDetails } from '../utils/environment';
import { FormField } from '../components/FormField';
import { SocialLoginButtons } from '../components/SocialLoginButtons';
import { useAuth } from '../hooks/useAuth';

export function LoginPage() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<Error | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const from = location.state?.from?.pathname || '/';

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setIsLoading(true);

    try {
      await login({ userName: username, password });
      navigate(from, { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err : new Error('Login failed'));
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-linear-to-br from-background to-content1 flex items-center justify-center p-4">
      <Card className="w-full max-w-md shadow-2xl">
        <CardHeader className="flex flex-col items-center pb-6 pt-8">
          <h1 className="text-4xl font-bold text-primary mb-2">Coinwarden</h1>
          <h2 className="text-2xl font-semibold text-foreground mb-2">Sign In</h2>
          <p className="text-default-600 text-center">Welcome back! Please sign in to your account.</p>
        </CardHeader>

        <CardContent className="px-8 pb-8">
          <form onSubmit={handleSubmit} className="space-y-6">
            {error && <ApiErrorDisplay error={error} title="Login Failed" showDetails={showErrorDetails} />}

            <FormField
              label="Username or Email"
              value={username}
              onChange={setUsername}
              isRequired
              isDisabled={isLoading}
              placeholder="Enter your username or email"
              autoComplete="username"
            />

            <FormField
              label="Password"
              type="password"
              value={password}
              onChange={setPassword}
              isRequired
              isDisabled={isLoading}
              placeholder="Enter your password"
              autoComplete="current-password"
            />

            <Button
              type="submit"
              className="w-full font-semibold"
              disabled={isLoading || !username || !password}
            >
              {isLoading && <Spinner color="current" size="sm" className="mr-2" />}
              {isLoading ? 'Signing In...' : 'Sign In'}
            </Button>
          </form>

          <Separator className="my-6" />

          <SocialLoginButtons isDisabled={isLoading} onError={setError} />

          <Separator className="my-6" />

          <div className="text-center space-y-2">
            <p className="text-default-600">
              Don't have an account?{' '}
              <Link to="/register" className="text-primary hover:text-primary-600 font-medium transition-colors">
                Sign up
              </Link>
            </p>
            <div>
              <Link to="/forgot-password" className="text-sm text-primary hover:text-primary-600 transition-colors">
                Forgot your password?
              </Link>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
