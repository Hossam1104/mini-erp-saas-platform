import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, ParamMap, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { vi } from 'vitest';
import { LanguageService } from '../../core/i18n/language.service';
import { MasterDataService } from '../master-data/master-data.service';
import { PurchaseRequestListItemResponse, PurchaseRequestOrganizationScopeResponse, PurchaseRequestResponse } from './purchase-request.model';
import { PurchaseRequestService } from './purchase-request.service';
import {
  SupplierQuotationComparisonResponse,
  SupplierQuotationListItemResponse,
  SupplierQuotationResponse,
} from './supplier-quotation.model';
import { SupplierQuotationService } from './supplier-quotation.service';
import { SupplierQuotationWorkspaceComponent } from './supplier-quotation-workspace.component';

const companyId = '22222222-2222-2222-2222-222222222222';
const purchaseRequestId = '11111111-1111-1111-1111-111111111111';
const quotationId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';

const organizationScope: PurchaseRequestOrganizationScopeResponse = {
  companyId,
  branchId: null,
  companyDisplayName: 'Acme Trading Co.',
  branchDisplayName: null,
  displayName: 'Acme Trading Co.',
};

const requestListItem: PurchaseRequestListItemResponse = {
  id: purchaseRequestId,
  companyId,
  branchId: null,
  requesterId: 'requester-1',
  status: 'Approved',
  purpose: 'Office restocking',
  lineCount: 1,
  createdAt: '2026-08-13T10:00:00Z',
  updatedAt: '2026-08-13T10:00:00Z',
  version: 'PRVERSION',
};

const request: PurchaseRequestResponse = {
  id: purchaseRequestId,
  tenantId: 'tenant-a',
  companyId,
  branchId: null,
  requesterId: 'requester-1',
  status: 'Approved',
  purpose: 'Office restocking',
  lines: [{
    id: 'line-1', productId: 'product-1', productSku: 'SKU-001', productName: 'Widget', unitOfMeasureId: 'uom-1',
    unitOfMeasureCode: 'PCS', quantity: 5, needByDate: '2026-09-01', purpose: 'Restocking', version: 'LINEVERSION',
  }],
  approval: null,
  createdAt: '2026-08-13T10:00:00Z',
  updatedAt: '2026-08-13T10:00:00Z',
  submittedAt: '2026-08-13T11:00:00Z',
  approvedAt: '2026-08-13T12:00:00Z',
  cancelledAt: null,
  version: 'PRVERSION',
  canEdit: false,
  canSubmit: false,
  canApprove: false,
  canReject: false,
  canReturnForChange: false,
  canCancel: false,
};

const quotationListItem: SupplierQuotationListItemResponse = {
  id: quotationId,
  purchaseRequestId,
  supplier: { id: 'supplier-1', code: 'SUP-001', name: 'Supplier One' },
  status: 'Submitted',
  supplierQuotationReference: 'SUP-Q-2026-001',
  offerDate: '2026-08-14',
  validUntil: '2026-09-14',
  currency: { id: 'currency-sar', code: 'SAR', name: 'Saudi Riyal' },
  commercialTotal: 575,
  coveredLineCount: 1,
  requestedLineCount: 1,
  hasEvidence: true,
  version: 'QVERSION',
};

const comparison: SupplierQuotationComparisonResponse = {
  purchaseRequestId,
  hasMixedCurrencies: false,
  directCurrencyComparisonAvailable: true,
  comparisonBasis: 'Server-calculated commercial total within currency groups',
  currencyGroups: [{ currencyId: 'currency-sar', currencyCode: 'SAR', supplierQuotationIds: [quotationId], directlyComparableWithinGroup: true }],
  quotations: [{
    supplierQuotationId: quotationId,
    supplier: quotationListItem.supplier,
    status: 'Submitted',
    supplierQuotationReference: quotationListItem.supplierQuotationReference,
    offerDate: quotationListItem.offerDate,
    validUntil: quotationListItem.validUntil,
    currency: quotationListItem.currency,
    commercialTotal: 575,
    coveredLineCount: 1,
    requestedLineCount: 1,
    hasEvidence: true,
    isDirectlyComparableToAll: true,
    paymentTermCode: 'NET30',
    deliveryTerms: 'DAP',
    offeredDeliveryDate: '2026-09-01',
    offeredDeliveryLeadTime: null,
    lines: [{
      purchaseRequestLineId: 'line-1', productSku: 'SKU-001', productName: 'Widget', requestedQuantity: 5, quotedQuantity: 5,
      unitPrice: 100, discountAmount: null, discountPercentage: null, taxRatePercentage: 15, taxAmount: 75,
      requestedNeedByDate: '2026-09-01', offeredDeliveryDate: '2026-09-01', isCovered: true, qualificationIssue: null,
    }],
    qualificationIssues: [],
  }],
  currentSourceDecision: null,
};

const quotation: SupplierQuotationResponse = {
  id: quotationId,
  tenantId: 'tenant-a',
  purchaseRequestId,
  companyId,
  branchId: null,
  createdByActorId: 'actor-1',
  supplier: quotationListItem.supplier,
  status: 'Draft',
  supplierQuotationReference: 'SUP-Q-2026-001',
  offerDate: '2026-08-14',
  validUntil: '2026-09-14',
  currency: quotationListItem.currency,
  paymentTerm: { id: 'payment-term-1', code: 'NET30', name: 'Net 30 days', version: 1 },
  deliveryTerms: 'DAP',
  offeredDeliveryDate: '2026-09-01',
  offeredDeliveryLeadTime: null,
  notes: 'Routine office restocking quote.',
  lines: [{
    id: 'quotation-line-1', purchaseRequestLineId: 'line-1', productId: 'product-1', productSku: 'SKU-001', productName: 'Widget',
    unitOfMeasureId: 'uom-1', unitOfMeasureCode: 'PCS', requestedQuantity: 5, quotedQuantity: 5, unitPrice: 100,
    discountAmount: null, discountPercentage: null, taxId: 'tax-1', taxCode: 'VAT', taxName: 'Configured VAT', taxRatePercentage: 15,
    taxAmount: 75, taxReference: null, requestedNeedByDate: '2026-09-01', offeredDeliveryDate: '2026-09-01', offeredDeliveryLeadTime: null,
    notes: null, version: 'LINEVERSION',
  }],
  evidence: [],
  createdAt: '2026-08-14T10:00:00Z',
  updatedAt: '2026-08-14T10:00:00Z',
  submittedAt: null,
  isSelected: false,
  version: 'QVERSION',
  canEdit: true,
  canSubmit: true,
  canWithdraw: false,
  canDisqualify: false,
};

function routeMap(id?: string): ParamMap {
  return convertToParamMap(id ? { id } : {});
}

describe('SupplierQuotationWorkspaceComponent', () => {
  let fixture: ComponentFixture<SupplierQuotationWorkspaceComponent>;
  let routeUrls: BehaviorSubject<Array<{ path: string }>>;
  let routeParams: BehaviorSubject<ParamMap>;
  let purchaseRequests: { list: ReturnType<typeof vi.fn>; get: ReturnType<typeof vi.fn>; organizationScopes: ReturnType<typeof vi.fn> };
  let quotations: {
    list: ReturnType<typeof vi.fn>;
    get: ReturnType<typeof vi.fn>;
    comparison: ReturnType<typeof vi.fn>;
    sourceDecision: ReturnType<typeof vi.fn>;
    sourceDecisionHistory: ReturnType<typeof vi.fn>;
    history: ReturnType<typeof vi.fn>;
    audit: ReturnType<typeof vi.fn>;
  };

  async function settle(): Promise<void> {
    await fixture.whenStable();
    await new Promise<void>((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  async function navigate(url: Array<{ path: string }>, id?: string): Promise<void> {
    routeParams.next(routeMap(id));
    routeUrls.next(url);
    await settle();
  }

  beforeEach(async () => {
    routeUrls = new BehaviorSubject([{ path: 'supplier-quotations' }]);
    routeParams = new BehaviorSubject(routeMap());
    purchaseRequests = {
      list: vi.fn(() => of([requestListItem])),
      get: vi.fn(() => of(request)),
      organizationScopes: vi.fn(() => of([organizationScope])),
    };
    quotations = {
      list: vi.fn(() => of([quotationListItem])),
      get: vi.fn(() => of(quotation)),
      comparison: vi.fn(() => of(comparison)),
      sourceDecision: vi.fn(() => of(null)),
      sourceDecisionHistory: vi.fn(() => of([])),
      history: vi.fn(() => of([])),
      audit: vi.fn(() => of([])),
    };

    await TestBed.configureTestingModule({
      imports: [SupplierQuotationWorkspaceComponent],
      providers: [
        provideRouter([]),
        LanguageService,
        { provide: ActivatedRoute, useValue: { url: routeUrls.asObservable(), snapshot: { get url() { return routeUrls.value; }, get paramMap() { return routeParams.value; } } } },
        { provide: PurchaseRequestService, useValue: purchaseRequests },
        { provide: SupplierQuotationService, useValue: quotations },
        { provide: MasterDataService, useValue: { list: vi.fn(() => of([])) } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(SupplierQuotationWorkspaceComponent);
    fixture.detectChanges();
    await settle();
  });

  it('renders a business-readable quotation list and filters loaded rows without raw GUID labels', () => {
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Supplier One');
    expect(text).toContain('Acme Trading Co.');
    expect(text).toContain('SUP-Q-2026-001');
    expect(text).not.toContain(companyId);
    expect(text).not.toContain('tenant-a');

    fixture.componentInstance.search.set('no-result');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain(TestBed.inject(LanguageService).text('supplierQuotationNoRecords'));
    expect(quotations.list).toHaveBeenCalledWith(purchaseRequestId);
  });

  it('starts create from an approved Purchase Request and shows server-provided lineage read-only', async () => {
    await navigate([{ path: 'supplier-quotations' }, { path: 'new' }]);

    expect(fixture.nativeElement.querySelector('[data-testid="quotation-request-select"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('input[name="companyId"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('input[name="branchId"]')).toBeNull();

    await fixture.componentInstance.selectPurchaseRequest(purchaseRequestId);
    await settle();
    expect(fixture.nativeElement.textContent).toContain('Acme Trading Co.');
    expect(fixture.nativeElement.textContent).toContain('SKU-001');
    expect(fixture.nativeElement.textContent).not.toContain(companyId);
  });

  it('gates lifecycle actions from server flags and exposes comparison/source-decision controls on detail', async () => {
    await navigate([{ path: 'supplier-quotations' }, quotationId ? { path: quotationId } : { path: '' }], quotationId);

    const text = fixture.nativeElement.textContent as string;
    const language = TestBed.inject(LanguageService);
    expect(text).toContain(language.text('supplierQuotationSubmit'));
    expect(text).not.toContain(language.text('supplierQuotationWithdraw'));
    expect(text).not.toContain(language.text('supplierQuotationDisqualify'));

    fixture.componentInstance.setTab('comparison');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('input[type="radio"][name="source-quotation"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('textarea')).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain(language.text('supplierQuotationRecordDecision'));
    expect(fixture.nativeElement.querySelector('.winner, [data-winner]')).toBeNull();
  });
});
