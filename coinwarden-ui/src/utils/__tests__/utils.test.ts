import { describe, it, expect } from 'vitest';
import { validatePassword, getPasswordRequirementsDescription } from '../passwordValidation';
import { handleOAuthCallback } from '../oauthUtils';

describe('validatePassword', () => {
  it('accepts a valid password', () => {
    const result = validatePassword('Secure1!');
    expect(result.isValid).toBe(true);
    expect(result.errors).toHaveLength(0);
  });

  it('rejects passwords shorter than 8 characters', () => {
    const result = validatePassword('Ab1!');
    expect(result.isValid).toBe(false);
    expect(result.errors).toContain('Password must be at least 8 characters long');
  });

  it('rejects passwords without a number', () => {
    const result = validatePassword('Password!');
    expect(result.isValid).toBe(false);
    expect(result.errors).toContain('Password must contain at least 1 number');
  });

  it('rejects passwords without a special character', () => {
    const result = validatePassword('Password1');
    expect(result.isValid).toBe(false);
    expect(result.errors).toContain('Password must contain at least 1 special character');
  });

  it('returns multiple errors for multiple violations', () => {
    const result = validatePassword('short');
    expect(result.isValid).toBe(false);
    expect(result.errors.length).toBeGreaterThan(1);
  });

  it('getPasswordRequirementsDescription returns a non-empty string', () => {
    expect(getPasswordRequirementsDescription()).toBeTruthy();
  });
});

describe('handleOAuthCallback', () => {
  it('returns error when error param present', () => {
    const params = new URLSearchParams({ error: 'access_denied', error_description: 'User denied' });
    const result = handleOAuthCallback(params);
    expect(result?.error).toBe('access_denied');
    expect(result?.errorDescription).toBe('User denied');
  });

  it('returns the code when present (state is validated server-side)', () => {
    const params = new URLSearchParams({ code: 'abc', state: 'ignored' });
    const result = handleOAuthCallback(params);
    expect(result).toEqual({ code: 'abc' });
  });
});
