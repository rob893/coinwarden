import { useId } from 'react';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { cn } from '@/lib/utils';

interface FormFieldProps {
  label?: string;
  name?: string;
  type?: string;
  value: string;
  onChange(value: string): void;
  isRequired?: boolean;
  isDisabled?: boolean;
  isInvalid?: boolean;
  placeholder?: string;
  description?: string;
  errorMessage?: string;
  autoComplete?: string;
  className?: string;
}

export function FormField({
  label,
  name,
  type = 'text',
  value,
  onChange,
  isRequired,
  isDisabled,
  isInvalid,
  placeholder,
  description,
  errorMessage,
  autoComplete,
  className
}: FormFieldProps) {
  const generatedId = useId();
  const fieldId = name ?? generatedId;

  return (
    <div className={cn('w-full space-y-1.5', className)}>
      {label && (
        <Label htmlFor={fieldId}>
          {label}
          {isRequired && <span className="text-destructive">*</span>}
        </Label>
      )}
      <Input
        id={fieldId}
        name={name}
        type={type}
        value={value}
        onChange={e => onChange(e.target.value)}
        required={isRequired}
        disabled={isDisabled}
        placeholder={placeholder}
        autoComplete={autoComplete}
        aria-invalid={isInvalid || undefined}
      />
      {description && !isInvalid && <p className="text-xs text-muted-foreground">{description}</p>}
      {isInvalid && errorMessage && <p className="text-xs text-destructive">{errorMessage}</p>}
    </div>
  );
}
