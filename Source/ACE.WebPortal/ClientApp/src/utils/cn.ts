import { type ClassValue, clsx } from "clsx";
import { twMerge } from "tailwind-merge";

/**
 * Utility for merging tailwind classes with clsx and tailwind-merge.
 * This resolves conflicts between tailwind classes (e.g. 'p-2 p-4' becomes 'p-4').
 */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}
