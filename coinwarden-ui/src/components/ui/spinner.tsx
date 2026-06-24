import type { ComponentProps } from 'react';
import { Loader2Icon } from 'lucide-react';
import { cn } from '@/lib/utils';

const sizeClasses = {
  sm: 'size-4',
  md: 'size-6',
  lg: 'size-8'
} as const;

type SpinnerProps = Omit<ComponentProps<typeof Loader2Icon>, 'color'> & {
  /** Spinner diameter token. */
  size?: keyof typeof sizeClasses;
  /** Color intent; `current` inherits the surrounding text color. */
  color?: 'accent' | 'current' | 'danger' | 'success' | 'warning' | 'default';
};

/**
 * Indeterminate loading spinner (lucide `Loader2`). Provides a small,
 * dependency-light replacement with size/color props for common intents.
 */
function Spinner({ size = 'md', color = 'accent', className, ...props }: SpinnerProps): React.JSX.Element {
  const colorClass =
    color === 'current'
      ? 'text-current'
      : color === 'danger'
        ? 'text-danger'
        : color === 'success'
          ? 'text-success'
          : color === 'warning'
            ? 'text-warning'
            : 'text-primary';

  return (
    <Loader2Icon
      role="status"
      aria-label="Loading"
      className={cn('animate-spin', sizeClasses[size], colorClass, className)}
      {...props}
    />
  );
}

export { Spinner };
