/**
 * Creates an `Idempotency-Key` for a POST request (plan 4.8). The API accepts 16 to 64 characters from
 * A-Z, a-z, 0-9, underscore or dash; a UUID without dashes is 32 hex characters.
 */
export function newIdempotencyKey(): string {
  return crypto.randomUUID().replace(/-/g, '');
}

/**
 * Keeps one key per unchanged request body. Retrying the same unchanged form reuses the key, so the server replays
 * the first outcome instead of creating a duplicate. A changed body, or a completed create via `reset`, starts a fresh key.
 */
export class IdempotencyKeyScope {
  private current: { key: string; body: string } | null = null;

  /** Returns the key for this body, creating a new one when the body differs from the last attempt. */
  keyFor(body: string): string {
    const current = this.current?.body === body ? this.current : { key: newIdempotencyKey(), body };
    this.current = current;
    return current.key;
  }

  /** Forgets the current key; call after a successful create so the next create gets a fresh key. */
  reset(): void {
    this.current = null;
  }
}
