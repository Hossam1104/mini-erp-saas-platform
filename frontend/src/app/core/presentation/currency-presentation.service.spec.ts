import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { ContextService } from '../context/context.service';
import { CurrencyPresentationService } from './currency-presentation.service';

describe('CurrencyPresentationService', () => {
  it('keeps configured SAR presentation separate from monetary behavior', () => {
    TestBed.configureTestingModule({
      providers: [
        CurrencyPresentationService,
        {
          provide: ContextService,
          useValue: {
            entry: signal({ currencyPresentation: {
              currencyCode: 'SAR',
              symbolAssetUrl: 'assets/country/saudi-riyal.svg',
              symbolTextFallback: 'SAR',
            } }),
          },
        },
      ],
    });
    const service = TestBed.inject(CurrencyPresentationService);

    expect(service.symbolAssetUrl('SAR')).toBe('assets/country/saudi-riyal.svg');
    expect(service.formatMoney(125.5, 'SAR', 'en-US')).toContain('SAR');
    expect(service.formatMoney(125.5, 'SAR', 'en-US')).toContain('125.50');
  });

  it('uses a semantic decimal fallback for valid non-ISO configured codes', () => {
    TestBed.configureTestingModule({
      providers: [
        CurrencyPresentationService,
        { provide: ContextService, useValue: { entry: signal(null) } },
      ],
    });
    const service = TestBed.inject(CurrencyPresentationService);

    const rendered = service.formatMoney(12.34, 'CUSTOM', 'en-US');
    expect(rendered).toContain('12.34');
    expect(rendered).toContain('CUSTOM');
    expect(service.symbolAssetUrl('USD')).toBeNull();
  });
});
