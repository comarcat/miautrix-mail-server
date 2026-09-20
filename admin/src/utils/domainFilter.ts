// Domain-scoped filtering shared by admin modules.
// The top-bar Domain picker uses '*' to mean "All Domains" (no filtering).

export const ALL_DOMAINS = '*';

/** Extract the domain portion of an email/address (part after '@'), lowercased. */
export function extractDomain(address: string | null | undefined): string {
  if (!address) return '';
  const at = address.lastIndexOf('@');
  return at >= 0 ? address.slice(at + 1).trim().toLowerCase() : '';
}

/**
 * True when the row should be shown for the active domain filter.
 * Returns true for the "All Domains" sentinel or an empty/undefined filter.
 * A row matches when any of its addresses belongs to the selected domain.
 */
export function matchesDomain(
  domainFilter: string | undefined,
  ...addresses: (string | null | undefined)[]
): boolean {
  if (!domainFilter || domainFilter === ALL_DOMAINS) return true;
  const target = domainFilter.trim().toLowerCase();
  return addresses.some((a) => extractDomain(a) === target);
}
