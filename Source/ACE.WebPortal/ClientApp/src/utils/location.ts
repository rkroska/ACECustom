/**
 * Converts a 32-bit landblock ID into a consistent 8-digit hex string.
 * This ensures standardized padding and casing across the portal.
 */
export const formatLandblockHex = (landblock: number | undefined | null): string => {
  if (landblock === undefined || landblock === null) return "00000000";
  return landblock.toString(16).toUpperCase().padStart(8, '0');
};

/**
 * Extracts the 16-bit normalized landblock ID (high prefix for outdoors/dungeons).
 * Uses unsigned right shift (>>>) to ensure 32-bit safety in JavaScript.
 */
export const getNormalizedLandblock = (landblock: number | undefined | null): number => {
  if (landblock === undefined || landblock === null) return 0;
  return (landblock >>> 16) || (landblock & 0xFFFF);
};

/**
 * Returns a 4-digit formatted hex string for a normalized landblock.
 */
export const formatNormalizedHex = (normalizedLandblock: number): string => {
  return (normalizedLandblock ?? 0).toString(16).toUpperCase().padStart(4, '0');
};
