import { IdempotencyKeyScope, newIdempotencyKey } from './idempotency-key';

describe('idempotency keys', () => {
  it('generates keys the API accepts: 16 to 64 characters from A-Z, a-z, 0-9, underscore or dash', () => {
    expect(newIdempotencyKey()).toMatch(/^[A-Za-z0-9_-]{16,64}$/);
  });

  it('generates a different key each time', () => {
    expect(newIdempotencyKey()).not.toBe(newIdempotencyKey());
  });

  it('reuses the same key while the request body is unchanged', () => {
    const scope = new IdempotencyKeyScope();

    const first = scope.keyFor('{"minPrice":0}');

    expect(scope.keyFor('{"minPrice":0}')).toBe(first);
  });

  it('issues a fresh key when the request body changes', () => {
    const scope = new IdempotencyKeyScope();
    const first = scope.keyFor('{"minPrice":0}');

    expect(scope.keyFor('{"minPrice":1}')).not.toBe(first);
  });

  it('issues a fresh key after reset, even for the same body', () => {
    const scope = new IdempotencyKeyScope();
    const first = scope.keyFor('{"minPrice":0}');

    scope.reset();

    expect(scope.keyFor('{"minPrice":0}')).not.toBe(first);
  });
});
