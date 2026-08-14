import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { SafeUiError, toSafeUiError } from '../../core/api/safe-error';
import {
  autoMatchColumns,
  buildNormalizedRows,
  parseImportFileContent,
  validateColumnMappings,
} from './import-parser';
import {
  ImportColumnMapping,
  ImportFormat,
  ImportMappingValidation,
  ImportParseResult,
  MasterDataImportAuditResponse,
  MasterDataImportBatchResponse,
  MasterDataImportDuplicatePolicy,
  MasterDataImportMode,
  MasterDataImportReconciliationResponse,
  MasterDataImportResourceKind,
  MasterDataImportRowInput,
  MasterDataImportRowResponse,
} from './master-data-import.models';
import { MasterDataImportService } from './master-data-import.service';

export interface ImportFileMetadata {
  name: string;
  size: number;
  format: ImportFormat;
}

@Injectable({ providedIn: 'root' })
export class MasterDataImportFacade {
  private readonly importService = inject(MasterDataImportService);

  // State Signals
  readonly selectedResourceKind = signal<MasterDataImportResourceKind | null>(null);
  readonly duplicatePolicy = signal<MasterDataImportDuplicatePolicy>('Reject');
  readonly importMode = signal<MasterDataImportMode>('DryRun');
  readonly fileMetadata = signal<ImportFileMetadata | null>(null);
  readonly parseResult = signal<ImportParseResult | null>(null);
  readonly columnMappings = signal<ImportColumnMapping[]>([]);
  readonly currentBatch = signal<MasterDataImportBatchResponse | null>(null);
  readonly batchRows = signal<MasterDataImportRowResponse[]>([]);
  readonly batchReconciliation = signal<MasterDataImportReconciliationResponse | null>(null);
  readonly batchAudit = signal<MasterDataImportAuditResponse[]>([]);
  readonly batchList = signal<MasterDataImportBatchResponse[]>([]);
  readonly busy = signal<boolean>(false);
  readonly error = signal<SafeUiError | null>(null);

  // Computed Signals
  readonly mappingValidation = computed<ImportMappingValidation | null>(() => {
    const kind = this.selectedResourceKind();
    const mappings = this.columnMappings();
    if (!kind || mappings.length === 0) return null;
    return validateColumnMappings(mappings, kind);
  });

  readonly normalizedRows = computed<MasterDataImportRowInput[]>(() => {
    const parse = this.parseResult();
    const mappings = this.columnMappings();
    const validation = this.mappingValidation();
    if (!parse || !parse.valid || !validation || !validation.valid) {
      return [];
    }
    return buildNormalizedRows(parse.rows, mappings);
  });

  readonly isReadyToSubmit = computed<boolean>(() => {
    return (
      this.selectedResourceKind() !== null &&
      this.parseResult()?.valid === true &&
      this.mappingValidation()?.valid === true &&
      this.normalizedRows().length > 0
    );
  });

  readonly canExecute = computed<boolean>(() => {
    const batch = this.currentBatch();
    return batch !== null && batch.status === 'Validated' && batch.mode === 'Commit';
  });

  readonly hasQuarantinedRows = computed<boolean>(() => {
    return this.batchRows().some((row) => row.outcome === 'Quarantined');
  });

  // Action Methods
  selectResource(kind: MasterDataImportResourceKind): void {
    this.selectedResourceKind.set(kind);
    this.clearError();
    // Re-evaluate column mappings if headers already exist
    const parse = this.parseResult();
    if (parse && parse.headers.length > 0) {
      this.columnMappings.set(autoMatchColumns(parse.headers, kind));
    }
  }

  setDuplicatePolicy(policy: MasterDataImportDuplicatePolicy): void {
    this.duplicatePolicy.set(policy);
  }

  setImportMode(mode: MasterDataImportMode): void {
    this.importMode.set(mode);
  }

  loadFileContent(content: string, fileName: string, fileSize = 0): void {
    this.clearError();
    const isJson =
      fileName.toLowerCase().endsWith('.json') ||
      content.trim().startsWith('{') ||
      content.trim().startsWith('[');
    const format: ImportFormat = isJson ? 'json' : 'csv';

    this.fileMetadata.set({
      name: fileName,
      size: fileSize || content.length,
      format,
    });

    const result = parseImportFileContent(content, fileName);
    this.parseResult.set(result);

    if (result.valid) {
      const kind = this.selectedResourceKind();
      if (kind) {
        this.columnMappings.set(autoMatchColumns(result.headers, kind));
      } else {
        this.columnMappings.set(result.headers.map((h) => ({ sourceColumn: h, targetField: null })));
      }
    } else {
      this.columnMappings.set([]);
      this.error.set({
        code: 'import_row_invalid',
        status: 400,
        correlationId: null,
      });
    }
  }

  updateMapping(sourceColumn: string, targetField: string | null): void {
    this.columnMappings.update((current) =>
      current.map((m) => (m.sourceColumn === sourceColumn ? { ...m, targetField } : m)),
    );
  }

  setMappings(mappings: ImportColumnMapping[]): void {
    this.columnMappings.set(mappings);
  }

  applyAutoMapping(): void {
    const parse = this.parseResult();
    const kind = this.selectedResourceKind();
    if (parse && kind) {
      this.columnMappings.set(autoMatchColumns(parse.headers, kind));
    }
  }

  async createBatch(sourceReference?: string): Promise<MasterDataImportBatchResponse | null> {
    const kind = this.selectedResourceKind();
    const rows = this.normalizedRows();
    const validation = this.mappingValidation();

    if (!kind || !validation || !validation.valid || rows.length === 0) {
      this.error.set({
        code: 'validation_failed',
        status: 400,
        correlationId: null,
      });
      return null;
    }

    this.busy.set(true);
    this.clearError();

    try {
      const batch = await this.importService.createBatch({
        resourceKind: kind,
        duplicatePolicy: this.duplicatePolicy(),
        mode: this.importMode(),
        sourceFileReference: sourceReference ?? this.fileMetadata()?.name ?? 'import-file',
        source: {
          sourceSystemCategory: 'angular-client',
          sourceFileReference: this.fileMetadata()?.name ?? null,
          batchReference: sourceReference ?? null,
        },
        rows,
      });

      this.currentBatch.set(batch);
      this.batchReconciliation.set(batch.reconciliation);
      await this.loadBatchRows(batch.id);
      return batch;
    } catch (err) {
      this.error.set(toSafeUiError(err));
      return null;
    } finally {
      this.busy.set(false);
    }
  }

  async simulateBatch(): Promise<MasterDataImportBatchResponse | null> {
    const batch = this.currentBatch();
    if (!batch) return null;

    this.busy.set(true);
    this.clearError();

    try {
      const simulated = await this.importService.simulate(batch.id);
      this.currentBatch.set(simulated);
      this.batchReconciliation.set(simulated.reconciliation);
      await this.loadBatchRows(simulated.id);
      return simulated;
    } catch (err) {
      this.error.set(toSafeUiError(err));
      return null;
    } finally {
      this.busy.set(false);
    }
  }

  async executeBatch(): Promise<MasterDataImportBatchResponse | null> {
    const batch = this.currentBatch();
    if (!batch) return null;

    this.busy.set(true);
    this.clearError();

    try {
      const executed = await this.importService.execute(batch.id, batch.version);
      this.currentBatch.set(executed);
      this.batchReconciliation.set(executed.reconciliation);
      await this.loadBatchRows(executed.id);
      return executed;
    } catch (err) {
      this.error.set(toSafeUiError(err));
      return null;
    } finally {
      this.busy.set(false);
    }
  }

  async loadBatch(batchId: string): Promise<void> {
    this.busy.set(true);
    this.clearError();

    try {
      const batch = await firstValueFrom(this.importService.getBatch(batchId));
      this.currentBatch.set(batch);
      this.selectedResourceKind.set(batch.resourceKind);
      this.duplicatePolicy.set(batch.duplicatePolicy);
      this.importMode.set(batch.mode);
      this.batchReconciliation.set(batch.reconciliation);
      await this.loadBatchRows(batchId);
    } catch (err) {
      this.error.set(toSafeUiError(err));
    } finally {
      this.busy.set(false);
    }
  }

  async loadBatchRows(batchId: string): Promise<void> {
    try {
      const rows = await firstValueFrom(this.importService.getRows(batchId));
      this.batchRows.set(rows);
    } catch (err) {
      this.error.set(toSafeUiError(err));
    }
  }

  async loadBatchReconciliation(batchId: string): Promise<void> {
    try {
      const reconciliation = await firstValueFrom(this.importService.getReconciliation(batchId));
      this.batchReconciliation.set(reconciliation);
    } catch (err) {
      this.error.set(toSafeUiError(err));
    }
  }

  async loadBatchAudit(batchId: string): Promise<void> {
    try {
      const audit = await firstValueFrom(this.importService.getAudit(batchId));
      this.batchAudit.set(audit);
    } catch (err) {
      this.error.set(toSafeUiError(err));
    }
  }

  async loadBatchEvidence(batchId: string): Promise<void> {
    this.busy.set(true);
    this.clearError();

    try {
      const evidence = await firstValueFrom(this.importService.getEvidence(batchId));
      this.currentBatch.set(evidence.batch);
      this.batchRows.set(evidence.rows);
      this.batchAudit.set(evidence.audit);
      this.batchReconciliation.set(evidence.reconciliation);
    } catch (err) {
      this.error.set(toSafeUiError(err));
    } finally {
      this.busy.set(false);
    }
  }

  async replayRow(
    rowId: string,
    correctedFields: Record<string, string | null>,
  ): Promise<MasterDataImportBatchResponse | null> {
    const batch = this.currentBatch();
    if (!batch) return null;

    this.busy.set(true);
    this.clearError();

    try {
      const replayed = await this.importService.replayQuarantinedRow(
        batch.id,
        rowId,
        { correctedFields },
        batch.version,
      );
      this.currentBatch.set(replayed);
      this.batchReconciliation.set(replayed.reconciliation);
      await this.loadBatchRows(replayed.id);
      return replayed;
    } catch (err) {
      this.error.set(toSafeUiError(err));
      return null;
    } finally {
      this.busy.set(false);
    }
  }

  async refreshBatchList(): Promise<void> {
    this.busy.set(true);
    this.clearError();

    try {
      const batches = await firstValueFrom(this.importService.listBatches());
      this.batchList.set(batches);
    } catch (err) {
      this.error.set(toSafeUiError(err));
    } finally {
      this.busy.set(false);
    }
  }

  clearError(): void {
    this.error.set(null);
  }

  reset(): void {
    this.selectedResourceKind.set(null);
    this.duplicatePolicy.set('Reject');
    this.importMode.set('DryRun');
    this.fileMetadata.set(null);
    this.parseResult.set(null);
    this.columnMappings.set([]);
    this.currentBatch.set(null);
    this.batchRows.set([]);
    this.batchReconciliation.set(null);
    this.batchAudit.set([]);
    this.busy.set(false);
    this.error.set(null);
  }
}
