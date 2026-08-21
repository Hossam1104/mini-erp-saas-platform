import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, ParamMap, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { vi } from 'vitest';
import { LanguageService } from '../../core/i18n/language.service';
import { ExchangeRateRecord } from '../master-data/master-data.models';
import { MasterDataService } from '../master-data/master-data.service';
import { GoodsReceiptService } from './goods-receipt.service';
import { PurchaseInvoiceHandoffResponse } from './purchase-invoice-handoff.model';
import { PurchaseInvoiceHandoffService } from './purchase-invoice-handoff.service';
import { PurchaseInvoiceMatchResponse } from './purchase-invoice-matching.model';
import { PurchaseInvoiceMatchingService } from './purchase-invoice-matching.service';
import { PurchaseInvoiceMatchingWorkspaceComponent } from './purchase-invoice-matching-workspace.component';
import { PurchaseOrderResponse } from './purchase-order.model';
import { PurchaseOrderService } from './purchase-order.service';

const handoff: PurchaseInvoiceHandoffResponse = {
  id: 'handoff-1',
  tenantId: 'tenant-a',
  companyId: 'company-a',
  branchId: null,
  purchaseOrderId: 'po-1',
  createdByActorId: 'actor-1',
  status: 'Recorded',
  supplierId: 'supplier-1',
  supplierCode: 'SUP-001',
  supplierName: 'Supplier One',
  currencyCode: 'EUR',
  supplierInvoiceReference: 'INV-001',
  supplierInvoiceDate: '2026-08-15',
  notes: null,
  createdAt: '2026-08-15T10:00:00Z',
  updatedAt: '2026-08-15T10:00:00Z',
  cancelledAt: null,
  cancellationReason: null,
  lines: [],
  sources: [],
  version: 'HANDOFF-V1',
  canCancel: false,
  declaredEvidence: {
    id: 'evidence-1',
    versionNumber: 1,
    supplierInvoiceReference: 'INV-001',
    supplierInvoiceDate: '2026-08-15',
    currencyCode: 'EUR',
    subtotalAmount: 100,
    discountAmount: 0,
    taxAmount: 15,
    grossAmount: 115,
    recordedAt: '2026-08-15T10:00:00Z',
    recordedByActorId: 'actor-1',
    lines: [],
  },
};

const purchaseOrder: PurchaseOrderResponse = {
  id: 'po-1',
  tenantId: 'tenant-a',
  companyId: 'company-a',
  branchId: null,
  createdByActorId: 'actor-1',
  status: 'Issued',
  source: {
    purchaseRequestId: 'pr-1',
    supplierQuotationId: 'quotation-1',
    sourceDecisionId: 'decision-1',
    purchaseRequestReference: 'PR-001',
    purchaseRequestPurpose: null,
    supplierQuotationReference: 'PO-001',
    supplier: { id: 'supplier-1', code: 'SUP-001', name: 'Supplier One' },
    currency: { id: 'currency-usd', code: 'USD', name: 'US Dollar' },
    paymentTerm: null,
    sourceDecisionRationale: 'Best value',
    selectedAt: '2026-08-10T10:00:00Z',
  },
  notes: null,
  createdAt: '2026-08-10T10:00:00Z',
  updatedAt: '2026-08-10T10:00:00Z',
  submittedAt: null,
  approvedAt: '2026-08-10T11:00:00Z',
  issuedAt: '2026-08-10T12:00:00Z',
  cancelledAt: null,
  latestConfirmationId: null,
  latestConfirmationStatus: null,
  approval: null,
  lines: [],
  pendingChanges: [],
  version: 'PO-V1',
  canEdit: false,
  canSubmit: false,
  canApprove: false,
  canReject: false,
  canReturnForChange: false,
  canIssue: false,
  canCancel: false,
  canCaptureConfirmation: false,
  canApproveSupplierChange: false,
  canRejectSupplierChange: false,
};

const match: PurchaseInvoiceMatchResponse = {
  id: 'match-1',
  tenantId: 'tenant-a',
  companyId: 'company-a',
  branchId: null,
  purchaseInvoiceHandoffId: 'handoff-1',
  purchaseOrderId: 'po-1',
  lifecycle: 'Current',
  result: 'NotMatchReady',
  evaluatedAt: '2026-08-15T10:00:00Z',
  evaluatedByActorId: 'actor-1',
  resolvedByActorId: null,
  resolvedAt: null,
  resolutionReason: null,
  sourceFingerprint: 'fingerprint',
  purchaseOrderVersion: 'PO-V1',
  handoffVersion: 'HANDOFF-V1',
  declaredEvidenceId: 'evidence-1',
  declaredEvidenceVersion: 1,
  policy: {
    policyId: 'exact-safe-default',
    version: 1,
    quantityAbsoluteTolerance: 0,
    quantityPercentageTolerance: 0,
    priceAbsoluteTolerance: 0,
    pricePercentageTolerance: 0,
    amountAbsoluteTolerance: 0,
    amountPercentageTolerance: 0,
    taxAbsoluteTolerance: 0,
    taxPercentageTolerance: 0,
    effectiveFrom: '0001-01-01T00:00:00Z',
    effectiveTo: null,
  },
  resolutionPolicy: null,
  appliedExchangeRate: null,
  variances: [],
  sourceSnapshot: null,
  version: 'MATCH-V1',
  varianceCount: 0,
};

const exchangeRate: ExchangeRateRecord = {
  id: 'rate-eur-usd',
  tenantId: 'tenant-a',
  lifecycleState: 'Active',
  version: 'RATE-V1',
  sourceCurrencyId: 'currency-eur',
  targetCurrencyId: 'currency-usd',
  sourceCurrencyCode: 'EUR',
  targetCurrencyCode: 'USD',
  currentVersionNumber: 2,
  versions: [
    {
      id: 'rate-version-2',
      versionNumber: 2,
      effectiveFrom: '2026-07-01',
      effectiveTo: null,
      rate: 1.1,
      rateScale: 1,
      provenance: 'Configured',
      sourceNotes: 'Finance-approved monthly rate',
      sourceCurrencyCode: 'EUR',
      targetCurrencyCode: 'USD',
    },
  ],
};

function routeMap(id?: string): ParamMap {
  return convertToParamMap(id ? { id } : {});
}

describe('PurchaseInvoiceMatchingWorkspaceComponent', () => {
  let fixture: ComponentFixture<PurchaseInvoiceMatchingWorkspaceComponent>;
  let routeParams: BehaviorSubject<ParamMap>;
  let matchingMock: { list: ReturnType<typeof vi.fn>; get: ReturnType<typeof vi.fn>; history: ReturnType<typeof vi.fn>; audit: ReturnType<typeof vi.fn>; evaluate: ReturnType<typeof vi.fn>; resolve: ReturnType<typeof vi.fn> };
  let handoffMock: { get: ReturnType<typeof vi.fn> };
  let masterDataMock: { list: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    routeParams = new BehaviorSubject(routeMap());
    matchingMock = {
      list: vi.fn(() => Promise.resolve([])),
      get: vi.fn(() => Promise.resolve(match)),
      history: vi.fn(() => Promise.resolve([])),
      audit: vi.fn(() => Promise.resolve([])),
      evaluate: vi.fn(() => Promise.resolve(match)),
      resolve: vi.fn(() => Promise.resolve(match)),
    };
    handoffMock = { get: vi.fn(() => of(handoff)) };
    masterDataMock = { list: vi.fn(() => of([exchangeRate])) };

    await TestBed.configureTestingModule({
      imports: [PurchaseInvoiceMatchingWorkspaceComponent],
      providers: [
        provideRouter([]),
        LanguageService,
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: routeParams.asObservable(),
            snapshot: { get paramMap() { return routeParams.value; } },
          },
        },
        { provide: PurchaseInvoiceMatchingService, useValue: matchingMock },
        { provide: PurchaseInvoiceHandoffService, useValue: handoffMock },
        { provide: PurchaseOrderService, useValue: { get: vi.fn(() => of(purchaseOrder)) } },
        { provide: GoodsReceiptService, useValue: { get: vi.fn(() => of(null)), warehouses: vi.fn(() => of([])) } },
        { provide: MasterDataService, useValue: masterDataMock },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PurchaseInvoiceMatchingWorkspaceComponent);
    fixture.detectChanges();
    await fixture.whenStable();
  });

  it('renders a selector for cross-currency evidence without exposing editable rate fields', () => {
    const component = fixture.componentInstance;
    component.mode.set('detail');
    component.match.set(match);
    component.handoff.set(handoff);
    component.purchaseOrder.set(purchaseOrder);
    component.exchangeRates.set([exchangeRate]);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="matching-exchange-rate-selector"] select')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('input')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('EUR → USD');
    expect(fixture.nativeElement.textContent).toContain('v2');
  });

  it('sends only the selected identity for cross-currency evaluation', async () => {
    const component = fixture.componentInstance;
    component.match.set(match);
    component.handoff.set(handoff);
    component.purchaseOrder.set(purchaseOrder);
    component.exchangeRates.set([exchangeRate]);
    component.selectExchangeRate(exchangeRate.id);

    await component.evaluateCurrent();

    expect(matchingMock.evaluate).toHaveBeenCalledWith('handoff-1', 'HANDOFF-V1', {
      exchangeRateReference: { exchangeRateId: 'rate-eur-usd' },
    });
  });

  it('resolves the exchange rate selector label to bilingual copy rather than the raw translation key', () => {
    const component = fixture.componentInstance;
    const lang = TestBed.inject(LanguageService);
    component.mode.set('detail');
    component.match.set(match);
    component.handoff.set(handoff);
    component.purchaseOrder.set(purchaseOrder);
    component.exchangeRates.set([exchangeRate]);
    fixture.detectChanges();

    const englishLabel = fixture.nativeElement.querySelector('.fx-selector span')?.textContent ?? '';
    expect(englishLabel).toContain('Select Exchange Rate');
    expect(englishLabel).not.toContain('selectExchangeRate');
    expect(fixture.nativeElement.textContent).not.toContain('selectExchangeRate');

    lang.setLanguage('ar');
    fixture.detectChanges();

    const arabicLabel = fixture.nativeElement.querySelector('.fx-selector span')?.textContent ?? '';
    expect(arabicLabel).toContain('اختر سعر الصرف');
    expect(arabicLabel).not.toContain('selectExchangeRate');
    expect(fixture.nativeElement.textContent).not.toContain('selectExchangeRate');

    lang.setLanguage('en');
  });

  it('does not request an exchange-rate reference when currencies already match', async () => {
    const component = fixture.componentInstance;
    const sameCurrencyHandoff = {
      ...handoff,
      currencyCode: 'USD',
      declaredEvidence: { ...handoff.declaredEvidence!, currencyCode: 'USD' },
    };
    component.match.set(match);
    component.handoff.set(sameCurrencyHandoff);
    component.purchaseOrder.set(purchaseOrder);
    component.selectExchangeRate(exchangeRate.id);
    handoffMock.get.mockReturnValue(of(sameCurrencyHandoff));

    await component.evaluateCurrent();

    expect(masterDataMock.list).not.toHaveBeenCalled();
    expect(matchingMock.evaluate).toHaveBeenCalledWith('handoff-1', 'HANDOFF-V1', {});
    expect(component.selectedExchangeRateId).toBe('');
  });
});
