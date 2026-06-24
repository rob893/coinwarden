import { lazy, Suspense } from 'react';
import { Routes, Route, Navigate } from 'react-router';
import { Spinner } from '@/components/ui/spinner';
import { AuthProvider } from './contexts/AuthContext';
import { ProtectedRoute } from './components/ProtectedRoute';
import { AppLayout } from './layouts/AppLayout';
import { routePaths } from './constants/routes';

const HomePage = lazy(() => import('./pages/HomePage').then(m => ({ default: m.HomePage })));
const LoginPage = lazy(() => import('./pages/LoginPage').then(m => ({ default: m.LoginPage })));
const RegisterPage = lazy(() => import('./pages/RegisterPage').then(m => ({ default: m.RegisterPage })));
const OAuthCallbackPage = lazy(() => import('./pages/OAuthCallbackPage').then(m => ({ default: m.OAuthCallbackPage })));
const ForgotPasswordPage = lazy(() => import('./pages/ForgotPasswordPage').then(m => ({ default: m.ForgotPasswordPage })));
const ResetPasswordPage = lazy(() => import('./pages/ResetPasswordPage').then(m => ({ default: m.ResetPasswordPage })));

function PageFallback() {
  return (
    <div className="flex min-h-screen items-center justify-center">
      <Spinner size="lg" color="accent" />
    </div>
  );
}

function App() {
  return (
    <AuthProvider>
      <div className="app min-h-screen bg-background text-foreground">
        <Suspense fallback={<PageFallback />}>
          <Routes>
            {/* Auth routes (unauthenticated) */}
            <Route path={routePaths.login} element={<LoginPage />} />
            <Route path={routePaths.register} element={<RegisterPage />} />
            <Route path={routePaths.forgotPassword} element={<ForgotPasswordPage />} />
            <Route path={routePaths.resetPassword} element={<ResetPasswordPage />} />
            <Route path={routePaths.gitHubCallback} element={<OAuthCallbackPage provider="github" />} />
            <Route path={routePaths.googleCallback} element={<OAuthCallbackPage provider="google" />} />

            {/* Protected routes */}
            <Route
              path={routePaths.home}
              element={
                <ProtectedRoute>
                  <AppLayout>
                    <HomePage />
                  </AppLayout>
                </ProtectedRoute>
              }
            />

            {/* Catch-all */}
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </Suspense>
      </div>
    </AuthProvider>
  );
}

export default App;
