import React from 'react';
import { Button } from '@/components/ui/button';
import { redirectToGitHubOAuth, redirectToGoogleOAuth } from '../utils/oauthUtils';
import { GitHubIcon, GoogleIcon } from './oauthIcons';

interface SocialLoginButtonsProps {
  /** Disables both buttons (e.g. while a primary form submission is in flight). */
  isDisabled?: boolean;
  /** Invoked when starting an OAuth flow fails (e.g. a provider client ID is not configured). */
  onError(error: Error): void;
}

/**
 * Renders the "or continue with" social login buttons (Google + GitHub) and owns
 * the OAuth-start click handlers. Shared by the Login and Register pages.
 */
export function SocialLoginButtons({ isDisabled, onError }: SocialLoginButtonsProps): React.JSX.Element {
  const handleGitHubLogin = () => {
    try {
      redirectToGitHubOAuth();
    } catch (err) {
      onError(err instanceof Error ? err : new Error('GitHub login setup error'));
    }
  };

  const handleGoogleLogin = () => {
    try {
      redirectToGoogleOAuth();
    } catch (err) {
      onError(err instanceof Error ? err : new Error('Google login setup error'));
    }
  };

  return (
    <div className="space-y-4">
      <p className="text-sm text-default-500 text-center mb-4">or continue with</p>

      <Button onClick={handleGoogleLogin} variant="outline" className="w-full font-semibold" disabled={isDisabled}>
        <GoogleIcon className="w-5 h-5 mr-2" />
        Continue with Google
      </Button>

      <Button onClick={handleGitHubLogin} variant="outline" className="w-full font-semibold" disabled={isDisabled}>
        <GitHubIcon className="w-5 h-5 mr-2" />
        Continue with GitHub
      </Button>
    </div>
  );
}
