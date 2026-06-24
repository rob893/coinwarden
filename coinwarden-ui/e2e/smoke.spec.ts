import { test, expect } from '@playwright/test';

test('app loads and shows login page', async ({ page }) => {
  await page.goto('/');
  // HashRouter redirects unauthenticated users to /login via ProtectedRoute
  await page.waitForURL(url => url.hash.includes('login') || url.hash === '' || url.pathname === '/', { timeout: 5000 });

  // The login page should render a heading
  const heading = page.getByRole('heading', { name: /sign in/i });
  await expect(heading).toBeVisible({ timeout: 5000 });
});
