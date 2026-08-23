import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { LanguageService } from '../../core/i18n/language.service';
import { InventoryService } from './inventory.service';
import { InventoryWarehouseOption } from './inventory.model';
import {
  InventoryFinanceValuationHandoff,
  InventoryValuationPolicy,
  InventoryValuationReconciliation,
  InventoryValuationSummary,
} from './inventory-valuation.model';
import { InventoryValuationService } from './inventory-valuation.service';
import { InventoryValuationWorkspaceComponent } from './inventory-valuation-workspace.component';

describe('InventoryValuationWorkspaceComponent', () => {
  let fixture: ComponentFixture<InventoryValuationWorkspaceComponent>;
  let valuationMock: Record<string, ReturnType<typeof vi.fn>>;
  let selectCurrentPolicyMock: ReturnType<typeof vi.fn>;
  let language: LanguageService;

  const warehouse: InventoryWarehouseOption = {
    tenantId: 'tenant-a',
    companyId: 'company-a',
    branchId: null,
    warehouseId: 'warehouse-a',
    code: 'WH-A',
    name: 'Main warehouse',
    displayName: 'WH-A · Main warehouse',
    isActive: true,
  };

  const policy: InventoryValuationPolicy = {
    id: 'policy-a', tenantId: 'tenant-a', companyId: 'company-a', functionalCurrencyId: 'currency-a', functionalCurrencyCode: 'SAR',
    scopeMode: 'WarehouseProductUom', effectiveFrom: '2026-01-01', effectiveTo: null, versionNumber: 1, unitCostScale: 8, amountScale: 8,
    roundingMode: 'ToEven', goodsReceiptCostBasis: 'PurchaseOrderUnitPrice', positiveAdjustmentCostBasis: 'CurrentMovingAverage', supplierReturnCostBasis: 'CurrentMovingAverage',
    isActive: true, version: 'v1', supersedesPolicyId: null,
  };

  const reconciliation = [
    { productId: 'product-a', physicalOnHandQuantity: 10, valuedQuantity: 10, valuedAmount: 100, status: 'Reconciled', averageUnitCost: 10 },
    { productId: 'product-b', physicalOnHandQuantity: 5, valuedQuantity: 5, valuedAmount: 50, status: 'Reconciled', averageUnitCost: 10 },
  ] as InventoryValuationReconciliation[];

  const handoffs = [
    { id: 'handoff-in', ledgerSequence: 1, direction: 'Inbound', quantity: 10, baseUnitCost: 10, baseAmount: 100, signedBaseAmount: 100, functionalCurrencyCode: 'SAR', status: 'ReadyForFinance', contractVersion: 'inventory-valuation-finance.v1', sourceType: 'OpeningBalance', sourceDocumentId: 'source-in' },
    { id: 'handoff-out', ledgerSequence: 2, direction: 'Outbound', quantity: 2, baseUnitCost: 10, baseAmount: 20, signedBaseAmount: -20, functionalCurrencyCode: 'SAR', status: 'ReadyForFinance', contractVersion: 'inventory-valuation-finance.v1', sourceType: 'StockIssue', sourceDocumentId: 'source-out' },
  ] as InventoryFinanceValuationHandoff[];

  function summary(overrides: Partial<InventoryValuationSummary> = {}): InventoryValuationSummary {
    return {
      tenantId: 'tenant-a', companyId: 'company-a', branchId: null, warehouseId: 'warehouse-a', functionalCurrencyCode: 'SAR',
      physicalOnHandQuantity: 15, valuedQuantity: 15, valuedAmount: 150, pendingMovementCount: 0, blockedMovementCount: 0,
      inTransitQuantity: 0, inTransitValue: 0, inTransitValueStatus: 'Ready', reconciliationStatus: 'Reconciled', latestLedgerSequence: 2,
      latestValuedLedgerSequence: 2, isComplete: true, isPartial: false, asOf: '2026-08-23T10:00:00Z', freshAsOf: '2026-08-23T10:00:00Z',
      ...overrides,
    };
  }

  async function create(summaryRecord = summary()): Promise<void> {
    const inventoryMock = { warehouses: vi.fn(() => of([warehouse])) };
    selectCurrentPolicyMock = vi.fn(() => policy);
    valuationMock = {
      policies: vi.fn(() => of([policy])),
      summary: vi.fn(() => of(summaryRecord)),
      reconciliation: vi.fn(() => of(reconciliation)),
      history: vi.fn(() => of([])),
      financeHandoffs: vi.fn(() => of(handoffs)),
      selectCurrentPolicy: selectCurrentPolicyMock,
      process: vi.fn(async () => ({ appliedCount: 1 })),
      export: vi.fn(() => of(new Blob())),
    };
    await TestBed.configureTestingModule({
      imports: [InventoryValuationWorkspaceComponent],
      providers: [
        provideRouter([]),
        LanguageService,
        { provide: InventoryService, useValue: inventoryMock },
        { provide: InventoryValuationService, useValue: valuationMock },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(InventoryValuationWorkspaceComponent);
    language = TestBed.inject(LanguageService);
    fixture.detectChanges();
    await vi.waitFor(() => expect(fixture.componentInstance.loading()).toBe(false));
    fixture.detectChanges();
  }

  it('renders the aggregate summary across two Products without a first-row total or aggregate average', async () => {
    await create();
    const element = fixture.nativeElement as HTMLElement;
    expect((fixture.componentInstance.summary() as InventoryValuationSummary).physicalOnHandQuantity).toBe(15);
    expect(element.querySelectorAll('[data-testid="valuation-summary-metrics"] .metric-card strong')[0].textContent?.trim()).toBe('15');
    expect(element.textContent).not.toContain('Average unit cost');

    fixture.componentInstance.setTab('reconciliation');
    fixture.detectChanges();
    expect(element.querySelectorAll('[data-testid="valuation-reconciliation"] tbody tr').length).toBe(2);
    expect(element.textContent).toContain('100.00');
    expect(element.textContent).toContain('50.00');
  });

  it('labels a pending Product as Partial/Pending and does not claim complete value', async () => {
    await create(summary({ valuedQuantity: 10, valuedAmount: 100, pendingMovementCount: 1, reconciliationStatus: 'PendingValuation', isComplete: false, isPartial: true }));
    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('Partial / pending evidence');
    expect(element.textContent).toContain('Pending');
    expect(element.textContent).not.toContain('Complete');
    expect(element.querySelector('[data-testid="valuation-summary-metrics"] .metric-card:nth-child(2) small')?.textContent?.trim()).toBe('Partial / pending evidence');
  });

  it('shows Finance handoff direction and signed amount meaning', async () => {
    await create();
    fixture.componentInstance.setTab('handoff');
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const handoff = element.querySelector('[data-testid="valuation-finance-handoff"]');
    expect(handoff?.textContent).toContain('Inbound');
    expect(handoff?.textContent).toContain('Outbound');
    expect(handoff?.textContent).toContain('-20.00');
    expect(handoff?.textContent).toContain('inventory-valuation-finance.v1');
  });

  it('preserves EN/AR language state and RTL direction on the valuation surface', async () => {
    await create();
    language.setLanguage('ar');
    fixture.detectChanges();
    expect(document.documentElement.lang).toBe('ar');
    expect(document.documentElement.dir).toBe('rtl');
    expect(fixture.nativeElement.textContent).toContain('المتوسط المتحرك');
    language.setLanguage('en');
    fixture.detectChanges();
    expect(document.documentElement.lang).toBe('en');
    expect(document.documentElement.dir).toBe('ltr');
  });

  it('uses the service current-policy selector rather than choosing a future version locally', async () => {
    await create();
    const selectedPolicy = fixture.componentInstance.policy();
    expect(selectCurrentPolicyMock).toHaveBeenCalledWith([policy]);
    expect(selectedPolicy?.versionNumber).toBe(1);
  });
});
