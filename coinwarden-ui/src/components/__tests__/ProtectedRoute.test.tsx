import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router';
import { ProtectedRoute } from '../ProtectedRoute';
import { useAuth } from '../../hooks/useAuth';

vi.mock('../../hooks/useAuth', () => ({
  useAuth: vi.fn()
}));

/** Builds a complete auth context value, overriding only the fields under test. */
function makeAuth(overrides: Partial<ReturnType<typeof useAuth>>): ReturnType<typeof useAuth> {
  return {
    user: null,
    isAuthenticated: false,
    isLoading: false,
    login: vi.fn(() => Promise.resolve()),
    loginWithGitHub: vi.fn(() => Promise.resolve()),
    loginWithGoogle: vi.fn(() => Promise.resolve()),
    register: vi.fn(() => Promise.resolve()),
    logout: vi.fn(() => Promise.resolve()),
    checkAuth: vi.fn(() => Promise.resolve()),
    ...overrides
  };
}

function renderProtected() {
  return render(
    <MemoryRouter initialEntries={['/protected']}>
      <Routes>
        <Route
          path="/protected"
          element={
            <ProtectedRoute>
              <div>Secret Content</div>
            </ProtectedRoute>
          }
        />
        <Route path="/login" element={<div>Login Page</div>} />
      </Routes>
    </MemoryRouter>
  );
}

describe('ProtectedRoute', () => {
  beforeEach(() => {
    vi.mocked(useAuth).mockReset();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('shows a loading state while auth is resolving', () => {
    vi.mocked(useAuth).mockReturnValue(makeAuth({ isLoading: true }));

    const { container } = renderProtected();

    expect(screen.queryByText('Secret Content')).not.toBeInTheDocument();
    expect(screen.queryByText('Login Page')).not.toBeInTheDocument();
    expect(container.querySelector('svg')).toBeInTheDocument();
  });

  it('redirects to the login route when unauthenticated', () => {
    vi.mocked(useAuth).mockReturnValue(makeAuth({ isLoading: false, isAuthenticated: false }));

    renderProtected();

    expect(screen.getByText('Login Page')).toBeInTheDocument();
    expect(screen.queryByText('Secret Content')).not.toBeInTheDocument();
  });

  it('renders the protected children when authenticated', () => {
    vi.mocked(useAuth).mockReturnValue(
      makeAuth({
        isLoading: false,
        isAuthenticated: true,
        user: { id: 1, userName: 'alice', email: 'alice@example.com', roles: [] }
      })
    );

    renderProtected();

    expect(screen.getByText('Secret Content')).toBeInTheDocument();
    expect(screen.queryByText('Login Page')).not.toBeInTheDocument();
  });
});
