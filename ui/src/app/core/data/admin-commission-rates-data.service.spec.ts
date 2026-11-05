import { provideHttpClient } from '@angular/common/http';
import type { TestRequest } from '@angular/common/http/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import type { CommissionRateRequest } from '../../api/models';
import { RuntimeConfigService } from '../config/runtime-config.service';
import { AdminCommissionRatesDataService } from './admin-commission-rates-data.service';

const KEY_PATTERN = /^[A-Za-z0-9_-]{16,64}$/;
const CREATE_URL = '/v1/admin/commission-rates';

const request: CommissionRateRequest = {
  maxPrice: 5000000,
  maxInclusive: false,
  ratePercent: 2,
};

describe('AdminCommissionRatesDataService.create idempotency', () => {
  let service: AdminCommissionRatesDataService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: RuntimeConfigService,
          useValue: { value: () => ({ apiBaseUrl: 'http://localhost:5049/api' }) },
        },
      ],
    });
    service = TestBed.inject(AdminCommissionRatesDataService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  function createRequests() {
    return httpMock.match((req) => req.method === 'POST' && req.url.endsWith(CREATE_URL));
  }

  it('sends an Idempotency-Key that the API accepts', async () => {
    const pending = service.create(request);

    const [sent] = createRequests() as [TestRequest];
    expect(sent.request.headers.get('Idempotency-Key')).toMatch(KEY_PATTERN);
    sent.flush({ id: 'rate-1' });
    await pending;
  });

  it('resends the same key when a failed create is retried with an unchanged form', async () => {
    const failed = service.create(request).catch((error: unknown) => error);
    const [first] = createRequests() as [TestRequest];
    first.flush({ title: 'Server error' }, { status: 500, statusText: 'Error' });
    await failed;

    const retry = service.create({ ...request });
    const [second] = createRequests() as [TestRequest];
    expect(second.request.headers.get('Idempotency-Key')).toBe(
      first.request.headers.get('Idempotency-Key'),
    );
    second.flush({ id: 'rate-1' });
    await retry;
  });

  it('sends a new key when the form changed before the retry', async () => {
    const failed = service.create(request).catch((error: unknown) => error);
    const [first] = createRequests() as [TestRequest];
    first.flush({ title: 'Server error' }, { status: 500, statusText: 'Error' });
    await failed;

    const changed = service.create({ ...request, ratePercent: 1.75 });
    const [second] = createRequests() as [TestRequest];
    expect(second.request.headers.get('Idempotency-Key')).not.toBe(
      first.request.headers.get('Idempotency-Key'),
    );
    second.flush({ id: 'rate-2' });
    await changed;
  });

  it('sends a new key for the next create after a successful one', async () => {
    const created = service.create(request);
    const [first] = createRequests() as [TestRequest];
    first.flush({ id: 'rate-1' });
    await created;

    const next = service.create(request);
    const [second] = createRequests() as [TestRequest];
    expect(second.request.headers.get('Idempotency-Key')).not.toBe(
      first.request.headers.get('Idempotency-Key'),
    );
    second.flush({ id: 'rate-2' });
    await next;
  });
});
