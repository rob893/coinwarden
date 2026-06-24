import { Component, type ErrorInfo, type ReactNode } from 'react';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';

interface ErrorBoundaryProps {
  children: ReactNode;
}

interface ErrorBoundaryState {
  hasError: boolean;
}

/**
 * Catches render-time errors anywhere in the child component tree and renders a
 * friendly fallback instead of blanking the entire SPA. Offers a reload action
 * so the user can recover without manually refreshing.
 */
export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  constructor(props: ErrorBoundaryProps) {
    super(props);
    this.state = { hasError: false };
  }

  /**
   * Updates state so the next render shows the fallback UI.
   * @returns The next error-boundary state.
   */
  static getDerivedStateFromError(): ErrorBoundaryState {
    return { hasError: true };
  }

  /**
   * Logs the captured error and component stack for diagnostics.
   * @param error The error thrown during rendering.
   * @param errorInfo Component stack information for the error.
   */
  componentDidCatch(error: Error, errorInfo: ErrorInfo): void {
    console.error('Uncaught error in component tree:', error, errorInfo);
  }

  /**
   * Reloads the page to attempt recovery from the error state.
   */
  handleReload(): void {
    window.location.reload();
  }

  render(): ReactNode {
    if (this.state.hasError) {
      return (
        <div className="min-h-screen bg-linear-to-br from-background to-content1 flex items-center justify-center p-4">
          <Card className="w-full max-w-md shadow-2xl">
            <CardHeader className="flex flex-col items-center pb-6 pt-8">
              <h1 className="text-3xl font-bold text-primary mb-4">Coinwarden</h1>
              <Badge variant="destructive">⚠️ Something went wrong</Badge>
            </CardHeader>

            <CardContent className="px-8 pb-8 text-center">
              <h2 className="text-2xl font-bold text-foreground mb-4">Unexpected Error</h2>
              <p className="text-default-600 mb-8 leading-relaxed">
                The application ran into an unexpected problem. Please reload the page to continue.
              </p>
              <Button className="w-full font-semibold" onClick={() => this.handleReload()}>
                Reload Page
              </Button>
            </CardContent>
          </Card>
        </div>
      );
    }

    return this.props.children;
  }
}
