import { describe, it, expect } from 'vitest';
import { ApiError } from '../../types/errors';

describe('ApiError', () => {
  it('constructs with basic message and status', () => {
    const err = new ApiError('Not found', 404, 'Not Found');
    expect(err.status).toBe(404);
    expect(err.statusText).toBe('Not Found');
    expect(err.message).toBe('Not found');
    expect(err.displayMessage).toBe('Not found');
    expect(err.allErrors).toEqual(['Not found']);
  });

  it('uses errors array from ProblemDetails when provided', () => {
    const err = new ApiError('Server error', 400, 'Bad Request', {
      type: 'about:blank',
      title: 'Bad Request',
      status: 400,
      detail: 'Validation failed',
      instance: '/api/test',
      extensions: {
        correlationId: 'corr-1',
        errors: ['Field X is required', 'Field Y is invalid'],
        traceId: 'trace-1'
      }
    });

    expect(err.displayMessage).toBe('Field X is required');
    expect(err.allErrors).toHaveLength(2);
    expect(err.correlationId).toBe('corr-1');
    expect(err.traceId).toBe('trace-1');
  });
});
