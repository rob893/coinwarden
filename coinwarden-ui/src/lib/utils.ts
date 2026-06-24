import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

/**
 * Merges class names with `clsx` and resolves Tailwind class conflicts via `tailwind-merge`.
 * @param inputs Class values (strings, arrays, conditionals) to combine.
 * @returns A single, de-duplicated class string.
 */
export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}
