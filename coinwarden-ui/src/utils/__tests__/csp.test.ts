import { describe, it, expect } from 'vitest';
import { buildContentSecurityPolicy } from '../csp';

describe('buildContentSecurityPolicy', () => {
  it('adds the API origin to connect-src when it is an absolute URL', () => {
    const csp = buildContentSecurityPolicy('https://api.example.com');

    expect(csp).toContain("connect-src 'self' https://api.example.com");
  });

  it('uses only the origin of the API base URL (drops any path)', () => {
    const csp = buildContentSecurityPolicy('https://api.example.com/api/v1/');

    expect(csp).toContain("connect-src 'self' https://api.example.com");
    expect(csp).not.toContain('/api/v1');
  });

  it("falls back to 'self' only when no API base URL is configured", () => {
    const csp = buildContentSecurityPolicy(undefined);

    expect(csp).toContain("connect-src 'self'");
    expect(csp).not.toContain('connect-src \'self\' http');
  });

  it("falls back to 'self' for a relative/malformed API base URL", () => {
    const csp = buildContentSecurityPolicy('/api');

    expect(csp).toContain("connect-src 'self'");
  });

  it('keeps script-src locked down (no unsafe-inline / unsafe-eval)', () => {
    const csp = buildContentSecurityPolicy('https://api.example.com');

    expect(csp).toContain("script-src 'self'");
    expect(csp).not.toContain("script-src 'self' 'unsafe-inline'");
    expect(csp).not.toContain('unsafe-eval');
  });

  it('includes hardening directives', () => {
    const csp = buildContentSecurityPolicy('https://api.example.com');

    expect(csp).toContain("default-src 'self'");
    expect(csp).toContain("object-src 'none'");
    expect(csp).toContain("base-uri 'self'");
    expect(csp).toContain("form-action 'self'");
    expect(csp).toContain("frame-ancestors 'none'");
  });

  it('allows inline styles for Tailwind', () => {
    const csp = buildContentSecurityPolicy('https://api.example.com');

    expect(csp).toContain("style-src 'self' 'unsafe-inline'");
  });
});
