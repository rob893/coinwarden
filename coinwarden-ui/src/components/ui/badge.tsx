import type { ComponentProps } from 'react';
import { Slot } from '@radix-ui/react-slot';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/utils';

const badgeVariants = cva(
  'inline-flex items-center justify-center rounded-md border px-2 py-0.5 text-xs font-medium w-fit whitespace-nowrap shrink-0 gap-1 [&>svg]:size-3 [&>svg]:pointer-events-none',
  {
    variants: {
      variant: {
        default: 'border-transparent bg-primary text-primary-foreground',
        secondary: 'border-transparent bg-secondary text-secondary-foreground',
        destructive: 'border-transparent bg-destructive/10 text-destructive',
        outline: 'text-foreground',
        success: 'border-transparent bg-success/15 text-success',
        warning: 'border-transparent bg-warning/15 text-warning'
      }
    },
    defaultVariants: {
      variant: 'default'
    }
  }
);

type BadgeProps = ComponentProps<'span'> &
  VariantProps<typeof badgeVariants> & {
    /** Render as the child element (via Radix `Slot`) instead of a `<span>`. */
    asChild?: boolean;
  };

/**
 * Small status/label pill. Extends the shadcn/ui badge with soft `success` and
 * `warning` variants so it can stand in for status chips.
 */
function Badge({ className, variant, asChild = false, ...props }: BadgeProps): React.JSX.Element {
  const Comp = asChild ? Slot : 'span';
  return <Comp data-slot="badge" className={cn(badgeVariants({ variant }), className)} {...props} />;
}

export { Badge, badgeVariants };
