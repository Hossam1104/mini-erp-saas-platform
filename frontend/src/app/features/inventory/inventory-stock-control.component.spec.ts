import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { MasterDataService } from '../master-data/master-data.service';
import { InventoryStockControlComponent } from './inventory-stock-control.component';
import { InventoryService } from './inventory.service';
import { InventoryCount, InventoryMovement, InventoryReasonCode } from './inventory.model';

describe('InventoryStockControlComponent', () => {
  let fixture: ComponentFixture<InventoryStockControlComponent>;
  let component: InventoryStockControlComponent;
  const inventory = {
    warehouses: () => of([]), products: () => of([]), reasonCodes: () => of([]), adjustments: () => of([]), counts: () => of([]), stockIssues: () => of([]), ledger: () => of([]),
    submitCount: vi.fn().mockResolvedValue({}),
    createReasonCode: vi.fn().mockResolvedValue({}), updateReasonCode: vi.fn().mockResolvedValue({}),
    adjustmentHistory: () => of([{ id: 'history-1', action: 'Posted', fromStatus: 'Approved', toStatus: 'Posted', occurredAt: '2026-08-22T10:00:00Z' }]),
    correctMovement: vi.fn().mockResolvedValue({}),
  } as unknown as InventoryService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [InventoryStockControlComponent],
      providers: [
        { provide: InventoryService, useValue: inventory },
        { provide: MasterDataService, useValue: { list: () => of([]) } },
        { provide: AuthService, useValue: { session: () => ({ actorId: 'counter-1' }) } },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(InventoryStockControlComponent);
    component = fixture.componentInstance;
    component.actorId = 'counter-1';
    fixture.detectChanges();
  });

  it('keeps the counter view blind and submits the entered physical quantity', async () => {
    const count = {
      id: 'count-1', assignedCounterId: 'counter-1', status: 'Draft', currentRoundGeneration: 1, countType: 'Cycle',
      lines: [{ id: 'line-1', isCurrentRound: true, productSku: 'SKU-A', productName: 'Product A', unitOfMeasureCode: 'EA', trackingIdentity: '', expectedQuantity: null, countedQuantity: null, variance: null }],
    } as unknown as InventoryCount;
    component.counts.set([count]);
    component.countedQuantities['line-1'] = 7;
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Expected:');
    expect(fixture.nativeElement.textContent).toContain('Submit observations');
    expect(fixture.nativeElement.textContent).not.toContain('Submit zero-variance');

    await component.submitCount(count);
    expect(inventory.submitCount).toHaveBeenCalledWith('count-1', undefined, { observations: [{ countLineId: 'line-1', countedQuantity: 7 }] });
  });

  it('wires reason catalogue maintenance and correction history without exposing unsupported sources', async () => {
    const reason = { id: 'reason-1', code: 'ADJ', englishName: 'Adjustment', arabicName: 'تعديل', category: 'Adjustment', isActive: true, version: 'AQ==' } as unknown as InventoryReasonCode;
    component.reasonDrafts[reason.id] = { englishName: 'Adjusted', arabicName: 'تم التعديل', category: 'Adjustment' };
    await component.updateReasonCode(reason);
    expect(inventory.updateReasonCode).toHaveBeenCalledWith('reason-1', 'AQ==', { englishName: 'Adjusted', arabicName: 'تم التعديل', category: 'Adjustment', isActive: true });

    const movement = { id: 'movement-1', sourceType: 'StockAdjustment', correctionOfMovementId: null, version: 'AQ==' } as unknown as InventoryMovement;
    component.ledgerMovements.set([movement]);
    component.reasonCatalogue.set([reason]);
    component.correctionReasonCodes[movement.id] = reason.code;
    await component.correctMovement(movement);
    expect(inventory.correctMovement).toHaveBeenCalledWith('movement-1', 'AQ==', 'ADJ', undefined);
    await component.loadAdjustmentHistory('adjustment-1');
    expect(component.historyFor('adjustment-1')).toHaveLength(1);
    expect(component.isEligibleCorrection({ ...movement, sourceType: 'GoodsReceipt' })).toBe(false);
  });
});
