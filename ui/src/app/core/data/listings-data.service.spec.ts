import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import type { CreateListingRequest } from '../../api/models';
import { RuntimeConfigService } from '../config/runtime-config.service';
import { ListingsDataService } from './listings-data.service';

const KEY_PATTERN = /^[A-Za-z0-9_-]{16,64}$/;

const request = {
  title: 'Two-bedroom flat',
  description: 'Bright',
  price: 6000000,
  propertyTypeId: '0190a1b2-0000-7000-8000-000000000001',
  city: 'Kathmandu',
  featureIds: [],
} as CreateListingRequest;

describe('ListingsDataService.create idempotency', () => {
  let service: ListingsDataService;
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
    service = TestBed.inject(ListingsDataService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  function createRequests() {
    return httpMock.match((req) => req.method === 'POST' && req.url.endsWith('/v1/listings'));
  }

  it('sends an Idempotency-Key that the API accepts without the caller supplying one', async () => {
    const pending = service.create(request);

    const [sent] = createRequests();
    expect(sent!.request.headers.get('Idempotency-Key')).toMatch(KEY_PATTERN);
    sent!.flush({ id: 'listing-1' });
    await pending;
  });

  it('resends the same key when a failed create is retried with an unchanged form', async () => {
    const failed = service.create(request).catch((error: unknown) => error);
    const [first] = createRequests();
    first!.flush({ title: 'Server error' }, { status: 500, statusText: 'Error' });
    await failed;

    const retry = service.create({ ...request });
    const [second] = createRequests();
    expect(second!.request.headers.get('Idempotency-Key')).toBe(
      first!.request.headers.get('Idempotency-Key'),
    );
    second!.flush({ id: 'listing-1' });
    await retry;
  });
});
