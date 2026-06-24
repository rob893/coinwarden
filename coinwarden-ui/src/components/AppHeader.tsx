import { Link, useNavigate } from 'react-router';
import { Button } from '@/components/ui/button';
import { useAuth } from '../hooks/useAuth';

export function AppHeader() {
  const { isAuthenticated, user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = async () => {
    await logout();
    navigate('/login', { replace: true });
  };

  return (
    <header className="w-full border-b border-divider bg-background">
      <div className="max-w-6xl mx-auto px-4 h-14 flex items-center justify-between">
        <Link to="/" className="font-bold text-xl text-primary no-underline">
          Coinwarden
        </Link>

        <nav className="flex items-center gap-3">
          {isAuthenticated && user ? (
            <>
              <span className="text-default-600 text-sm hidden sm:inline">{user.userName}</span>
              <Button variant="destructive" size="sm" onClick={handleLogout}>
                Sign Out
              </Button>
            </>
          ) : (
            <>
              <Link
                to="/login"
                className="text-sm font-medium text-default-600 hover:text-primary transition-colors px-3 py-1"
              >
                Sign In
              </Link>
              <Link
                to="/register"
                className="inline-flex items-center justify-center rounded-lg bg-primary px-3 py-1.5 text-sm font-semibold text-primary-foreground hover:bg-primary/90 transition-colors"
              >
                Sign Up
              </Link>
            </>
          )}
        </nav>
      </div>
    </header>
  );
}
