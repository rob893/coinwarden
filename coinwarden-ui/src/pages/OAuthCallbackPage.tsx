import { useEffect, useState, useRef } from 'react';
import { useNavigate } from 'react-router';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Spinner } from '@/components/ui/spinner';
import { useAuth } from '../hooks/useAuth';
import { GitHubIcon, GoogleIcon } from '../components/oauthIcons';
import { handleOAuthCallbackFromUrl, type OAuthProvider } from '../utils/oauthUtils';

interface ProviderConfig {
  name: string;
  icon: React.JSX.Element;
  badgeVariant: 'default' | 'secondary' | 'success' | 'warning' | 'destructive';
}

const providerConfigs: Record<OAuthProvider, ProviderConfig> = {
  github: {
    name: 'GitHub',
    badgeVariant: 'secondary',
    icon: <GitHubIcon className="w-8 h-8 text-foreground" />
  },
  google: {
    name: 'Google',
    badgeVariant: 'default',
    icon: <GoogleIcon className="w-8 h-8" />
  }
};

export function OAuthCallbackPage({ provider }: { provider: OAuthProvider }) {
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [step, setStep] = useState<'processing' | 'exchanging' | 'authenticating' | 'success' | 'error'>('processing');
  const { loginWithGitHub, loginWithGoogle } = useAuth();
  const navigate = useNavigate();
  const hasProcessed = useRef(false);

  const providerConfig = providerConfigs[provider];

  useEffect(() => {
    if (hasProcessed.current) return;
    hasProcessed.current = true;

    const processCallback = async () => {
      try {
        setStep('processing');

        const result = handleOAuthCallbackFromUrl();

        // Scrub the single-use OAuth `code`/`state` from the URL and history once they have been
        // read and CSRF-verified, so they no longer linger in the address bar or window.history.
        // HashRouter keeps the route in the hash, so preserve the route portion and drop the query.
        const cleanHash = window.location.hash.split('?')[0];
        window.history.replaceState(null, '', window.location.pathname + window.location.search + cleanHash);

        if (!result) {
          setError('Invalid callback parameters');
          setStep('error');
          return;
        }

        if (result.error) {
          setError(result.errorDescription || result.error);
          setStep('error');
          return;
        }

        if (!result.code) {
          setError('No authorization code received');
          setStep('error');
          return;
        }

        setStep('exchanging');
        await new Promise(resolve => setTimeout(resolve, 500));
        setStep('authenticating');

        if (provider === 'github') {
          await loginWithGitHub(result.code);
        } else {
          await loginWithGoogle(result.code);
        }

        setStep('success');
        await new Promise(resolve => setTimeout(resolve, 800));
        navigate('/', { replace: true });
      } catch (err) {
        setError(err instanceof Error ? err.message : `${providerConfig?.name || 'OAuth'} login failed`);
        setStep('error');
      } finally {
        setIsLoading(false);
      }
    };

    processCallback();
  }, [loginWithGitHub, loginWithGoogle, navigate, provider, providerConfig?.name]);

  const stepLabel = {
    processing: 'Processing Callback',
    exchanging: 'Exchanging Tokens',
    authenticating: 'Authenticating',
    success: 'Login Successful!',
    error: 'Login Failed'
  }[step];

  const stepDesc = {
    processing: `Validating ${providerConfig?.name || 'OAuth'} authorization...`,
    exchanging: 'Securely exchanging authorization code...',
    authenticating: 'Completing your login...',
    success: 'Redirecting you now...',
    error: `Something went wrong during the ${providerConfig?.name || 'OAuth'} login process.`
  }[step];

  if (!providerConfig) {
    return (
      <div className="min-h-screen flex items-center justify-center p-4">
        <Card className="w-full max-w-md shadow-2xl">
          <CardContent className="px-8 py-8 text-center">
            <span className="text-5xl mb-4 block">❌</span>
            <h2 className="text-2xl font-bold text-foreground mb-2">Invalid Provider</h2>
            <p className="text-default-600 mb-6">Unsupported OAuth provider: {provider}</p>
            <Button className="w-full" onClick={() => navigate('/login')}>Return to Login</Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-linear-to-br from-background via-content1 to-background flex items-center justify-center p-4">
      <Card className="w-full max-w-md shadow-2xl">
        <CardHeader className="flex flex-col items-center pb-6 pt-8">
          <div className="mb-4 p-3 rounded-full bg-content2 border border-divider">{providerConfig.icon}</div>
          <Badge variant={providerConfig.badgeVariant}>{providerConfig.name} Authentication</Badge>
        </CardHeader>

        <CardContent className="px-8 pb-8 text-center">
          <div className="flex justify-center mb-6">
            {step === 'success' ? <span className="text-5xl">✅</span>
              : step === 'error' ? <span className="text-5xl">❌</span>
              : <Spinner size="lg" color="accent" />}
          </div>

          <div className="space-y-3 mb-6">
            <h2 className="text-2xl font-bold text-foreground">{stepLabel}</h2>
            <p className="text-default-600 text-lg">{stepDesc}</p>
          </div>

          {isLoading && step !== 'error' && (
            <div className="flex justify-center items-center space-x-2 mb-6">
              {['processing', 'exchanging', 'authenticating'].map((_s, i) => (
                <div
                  key={i}
                  className={`w-2 h-2 rounded-full transition-colors ${['processing', 'exchanging', 'authenticating', 'success'].indexOf(step) >= i ? 'bg-primary' : 'bg-content3'}`}
                />
              ))}
              <div className={`w-2 h-2 rounded-full ${step === 'success' ? 'bg-success' : 'bg-content3'}`} />
            </div>
          )}

          {error && step === 'error' && (
            <div className="mb-6 p-4 bg-danger/10 border border-danger/20 rounded-lg">
              <p className="text-danger text-sm font-medium">{error}</p>
            </div>
          )}

          {step === 'error' && (
            <Button className="w-full font-semibold" onClick={() => navigate('/login')}>
              Return to Login
            </Button>
          )}

          {isLoading && step !== 'error' && (
            <p className="text-sm text-default-500">This may take a few moments...</p>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
