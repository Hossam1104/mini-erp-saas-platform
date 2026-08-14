export type MasterDataImportMode = 'DryRun' | 'Commit';

export type MasterDataImportStatus =
  | 'Draft'
  | 'Simulating'
  | 'Validated'
  | 'Completed'
  | 'CompletedWithErrors'
  | 'Failed';

export type MasterDataImportDuplicatePolicy =
  | 'Reject'
  | 'SkipExisting'
  | 'UpdateMutableFields';

export type MasterDataImportRowOutcome =
  | 'NotProcessed'
  | 'Accepted'
  | 'Rejected'
  | 'Quarantined';

export type MasterDataImportDiagnosticSeverity = 'Info' | 'Warning' | 'Error';

export type MasterDataImportMutationDisposition =
  | 'NotAttempted'
  | 'Eligible'
  | 'SkippedExisting'
  | 'Committed'
  | 'Updated'
  | 'Failed';

export type MasterDataImportResourceKind =
  | 'Product'
  | 'ProductCategory'
  | 'UnitOfMeasure'
  | 'Supplier'
  | 'BusinessCustomer'
  | 'PriceList'
  | 'Tax'
  | 'PaymentTerm'
  | 'Currency'
  | 'ExchangeRate'
  | 'Import';

export interface MasterDataImportSourceRequest {
  sourceSystemCategory?: string | null;
  sourceFileReference?: string | null;
  batchReference?: string | null;
}

export interface MasterDataImportRowInput {
  rowNumber: number;
  fields?: Record<string, string | null> | null;
}

export interface MasterDataImportBatchRequest {
  resourceKind: MasterDataImportResourceKind;
  source?: MasterDataImportSourceRequest | null;
  sourceFileReference?: string | null;
  duplicatePolicy: MasterDataImportDuplicatePolicy;
  mode: MasterDataImportMode;
  rows?: MasterDataImportRowInput[] | null;
}

export interface MasterDataImportReplayRequest {
  correctedFields?: Record<string, string | null> | null;
}

export interface MasterDataImportDiagnosticResponse {
  code: string;
  message: string;
  field: string | null;
  severity: MasterDataImportDiagnosticSeverity;
}

export interface MasterDataImportReconciliationResponse {
  totalRows: number;
  accepted: number;
  rejected: number;
  quarantined: number;
  committed: number;
  skipped: number;
  failed: number;
  isConsistent: boolean;
  formula: string;
}

export interface MasterDataImportBatchResponse {
  id: string;
  tenantId: string;
  resourceKind: MasterDataImportResourceKind;
  source: MasterDataImportSourceRequest;
  duplicatePolicy: MasterDataImportDuplicatePolicy;
  mode: MasterDataImportMode;
  status: MasterDataImportStatus;
  submittedActorId: string;
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
  correlationId: string;
  totalRows: number;
  acceptedCount: number;
  rejectedCount: number;
  quarantinedCount: number;
  committedCount: number;
  skippedCount: number;
  failedCount: number;
  idempotencyKey: string | null;
  version: string;
  reconciliation: MasterDataImportReconciliationResponse;
}

export interface MasterDataImportRowResponse {
  id: string;
  batchId: string;
  originalRowNumber: number;
  replaySequence: number;
  isCurrent: boolean;
  resourceKind: MasterDataImportResourceKind;
  sourceFields: Record<string, string | null>;
  normalizedFields: Record<string, string | null>;
  outcome: MasterDataImportRowOutcome;
  diagnostics: MasterDataImportDiagnosticResponse[];
  highestSeverity: MasterDataImportDiagnosticSeverity;
  mutationDisposition: MasterDataImportMutationDisposition;
  resultingResourceId: string | null;
  resultingResourceCode: string | null;
  replayOfRowId: string | null;
  originalRowId: string | null;
  processedAt: string;
  version: string;
}

export interface MasterDataImportAuditResponse {
  evidenceId: string;
  occurredAt: string;
  operationId: string;
  correlationId: string;
  tenantId: string;
  actorId: string;
  batchId: string;
  rowId: string | null;
  originalRowNumber: number | null;
  resourceKind: MasterDataImportResourceKind;
  outcome: string;
  sourceReference: string;
  detail: string | null;
}

export interface MasterDataImportStatusResponse {
  id: string;
  status: MasterDataImportStatus;
  correlationId: string;
  version: string;
}

export interface MasterDataImportEvidenceResponse {
  batch: MasterDataImportBatchResponse;
  rows: MasterDataImportRowResponse[];
  audit: MasterDataImportAuditResponse[];
  reconciliation: MasterDataImportReconciliationResponse;
}

// ---------------------------------------------------------------------------
// Nonvisual Parsing, Resource & Column Mapping Definitions
// ---------------------------------------------------------------------------

export type ImportFormat = 'csv' | 'json';

export type ImportFieldType =
  | 'string'
  | 'guid'
  | 'number'
  | 'boolean'
  | 'date'
  | 'enum'
  | 'json'
  | 'stringList';

export interface ImportFieldDefinition {
  name: string;
  label: string;
  required: boolean;
  type: ImportFieldType;
  description?: string;
  sampleValue?: string;
  aliases?: string[];
}

export interface ImportResourceDefinition {
  kind: MasterDataImportResourceKind;
  key: string;
  label: string;
  allowsMutableUpdate: boolean;
  fields: ImportFieldDefinition[];
}

export interface ImportParsedRow {
  rowNumber: number;
  rawFields: Record<string, string | null>;
}

export interface ImportParseResult {
  valid: boolean;
  format: ImportFormat;
  headers: string[];
  rows: ImportParsedRow[];
  totalRows: number;
  error?: string | null;
}

export interface ImportColumnMapping {
  sourceColumn: string;
  targetField: string | null;
}

export interface ImportMappingValidation {
  valid: boolean;
  errors: string[];
  missingRequiredFields: string[];
  duplicateMappings: string[];
  mappedFieldsCount: number;
}

export const IMPORT_RESOURCE_DEFINITIONS: readonly ImportResourceDefinition[] = [
  {
    kind: 'ProductCategory',
    key: 'categories',
    label: 'Category',
    allowsMutableUpdate: true,
    fields: [
      { name: 'code', label: 'Category Code', required: true, type: 'string', sampleValue: 'CAT-001', aliases: ['categorycode', 'category_code'] },
      { name: 'englishName', label: 'English Name', required: false, type: 'string', sampleValue: 'Electronics', aliases: ['name', 'categoryname', 'category_name', 'name_en', 'nameenglish'] },
      { name: 'arabicName', label: 'Arabic Name', required: false, type: 'string', sampleValue: 'إلكترونيات', aliases: ['name_ar', 'namearabic'] },
      { name: 'parentCategoryId', label: 'Parent Category ID', required: false, type: 'guid', sampleValue: 'b5c5e8c1-1111-4444-8888-aaaaaaaaaaaa', aliases: ['parentcategory', 'parent_category_id'] },
      { name: 'trackingDefaultEnabled', label: 'Default Batch/Serial Tracking', required: false, type: 'boolean', sampleValue: 'false', aliases: ['tracking', 'tracking_default', 'trackingdefault'] },
    ],
  },
  {
    kind: 'UnitOfMeasure',
    key: 'units',
    label: 'Unit of Measure',
    allowsMutableUpdate: true,
    fields: [
      { name: 'code', label: 'UOM Code', required: true, type: 'string', sampleValue: 'PCS', aliases: ['uomcode', 'uom_code', 'unitcode'] },
      { name: 'englishName', label: 'English Name', required: false, type: 'string', sampleValue: 'Pieces', aliases: ['name', 'uomname', 'name_en', 'nameenglish'] },
      { name: 'arabicName', label: 'Arabic Name', required: false, type: 'string', sampleValue: 'قطعة', aliases: ['name_ar', 'namearabic'] },
    ],
  },
  {
    kind: 'Product',
    key: 'products',
    label: 'Product',
    allowsMutableUpdate: true,
    fields: [
      { name: 'sku', label: 'Product SKU', required: true, type: 'string', sampleValue: 'SKU-1001', aliases: ['code', 'productcode', 'product_sku'] },
      { name: 'englishName', label: 'English Name', required: true, type: 'string', sampleValue: 'Industrial Valve 10mm', aliases: ['name', 'productname', 'name_en', 'nameenglish'] },
      { name: 'categoryId', label: 'Category ID', required: true, type: 'guid', sampleValue: 'b5c5e8c1-1111-4444-8888-aaaaaaaaaaaa', aliases: ['category', 'category_id'] },
      { name: 'baseUnitOfMeasureId', label: 'Base Unit of Measure ID', required: true, type: 'guid', sampleValue: 'c7d7f9d2-2222-4444-8888-bbbbbbbbbbbb', aliases: ['uomid', 'unitid', 'baseuomid', 'base_unit_of_measure_id'] },
      { name: 'arabicName', label: 'Arabic Name', required: false, type: 'string', sampleValue: 'صمام صناعي 10 مم', aliases: ['name_ar', 'namearabic'] },
      { name: 'description', label: 'Description', required: false, type: 'string', sampleValue: 'Standard heavy duty brass valve', aliases: ['desc', 'details'] },
      { name: 'barcodes', label: 'Barcodes (CSV or JSON Array)', required: false, type: 'stringList', sampleValue: '6281001234567,6281001234568', aliases: ['barcode', 'barcode_list'] },
      { name: 'trackingEnabledOverride', label: 'Tracking Override (true/false/null)', required: false, type: 'boolean', sampleValue: 'false', aliases: ['trackingoverride', 'tracking_override'] },
      { name: 'isSellable', label: 'Sellable Flag', required: false, type: 'boolean', sampleValue: 'true', aliases: ['sellable', 'is_sellable'] },
      { name: 'isPurchasable', label: 'Purchasable Flag', required: false, type: 'boolean', sampleValue: 'true', aliases: ['purchasable', 'is_purchasable'] },
      { name: 'isInventoryRelevant', label: 'Inventory Relevant', required: false, type: 'boolean', sampleValue: 'true', aliases: ['inventory', 'is_inventory_relevant'] },
    ],
  },
  {
    kind: 'Supplier',
    key: 'suppliers',
    label: 'Supplier',
    allowsMutableUpdate: true,
    fields: [
      { name: 'code', label: 'Supplier Code', required: true, type: 'string', sampleValue: 'SUP-001', aliases: ['suppliercode', 'vendorcode', 'vendor_code'] },
      { name: 'legalNameEnglish', label: 'English Legal Name', required: true, type: 'string', sampleValue: 'Gulf Supplies Ltd', aliases: ['name', 'legalname', 'legalname_en', 'name_en'] },
      { name: 'legalNameArabic', label: 'Arabic Legal Name', required: false, type: 'string', sampleValue: 'شركة التوريدات الخليجية', aliases: ['legalname_ar', 'name_ar'] },
      { name: 'tradingNameEnglish', label: 'English Trading Name', required: false, type: 'string', sampleValue: 'Gulf Supplies', aliases: ['tradingname', 'tradingname_en'] },
      { name: 'tradingNameArabic', label: 'Arabic Trading Name', required: false, type: 'string', sampleValue: 'التوريدات الخليجية', aliases: ['tradingname_ar'] },
      { name: 'registrationReference', label: 'Commercial / VAT Registration', required: false, type: 'string', sampleValue: 'CR-1010203040', aliases: ['cr', 'vat_number', 'taxnumber', 'registration'] },
      { name: 'contacts', label: 'Contacts (JSON Array)', required: false, type: 'json', sampleValue: '[{"name":"Ali","email":"ali@gulf.example","phone":"+966501112233"}]', aliases: ['contact_list', 'contactpersons'] },
    ],
  },
  {
    kind: 'BusinessCustomer',
    key: 'customers',
    label: 'Business Customer',
    allowsMutableUpdate: true,
    fields: [
      { name: 'code', label: 'Customer Code', required: true, type: 'string', sampleValue: 'CUST-001', aliases: ['customercode', 'clientcode', 'client_code'] },
      { name: 'legalNameEnglish', label: 'English Legal Name', required: true, type: 'string', sampleValue: 'Al-Noor Trading Est', aliases: ['name', 'legalname', 'legalname_en', 'name_en'] },
      { name: 'legalNameArabic', label: 'Arabic Legal Name', required: false, type: 'string', sampleValue: 'مؤسسة النور التجارية', aliases: ['legalname_ar', 'name_ar'] },
      { name: 'tradingNameEnglish', label: 'English Trading Name', required: false, type: 'string', sampleValue: 'Al-Noor Trading', aliases: ['tradingname', 'tradingname_en'] },
      { name: 'tradingNameArabic', label: 'Arabic Trading Name', required: false, type: 'string', sampleValue: 'النور للتجارة', aliases: ['tradingname_ar'] },
      { name: 'contacts', label: 'Contacts (JSON Array)', required: false, type: 'json', sampleValue: '[{"name":"Omar","email":"omar@alnoor.example","phone":"+966504445566"}]', aliases: ['contact_list', 'contactpersons'] },
    ],
  },
  {
    kind: 'Currency',
    key: 'currencies',
    label: 'Currency',
    allowsMutableUpdate: true,
    fields: [
      { name: 'code', label: 'ISO Currency Code', required: true, type: 'string', sampleValue: 'SAR', aliases: ['currencycode', 'isocode', 'currency'] },
      { name: 'englishName', label: 'English Name', required: false, type: 'string', sampleValue: 'Saudi Riyal', aliases: ['name', 'currencyname', 'name_en'] },
      { name: 'arabicName', label: 'Arabic Name', required: false, type: 'string', sampleValue: 'ريال سعودي', aliases: ['name_ar'] },
    ],
  },
  {
    kind: 'PaymentTerm',
    key: 'payment-terms',
    label: 'Payment Term',
    allowsMutableUpdate: false,
    fields: [
      { name: 'code', label: 'Payment Term Code', required: true, type: 'string', sampleValue: 'NET30', aliases: ['termcode', 'paymenttermcode'] },
      { name: 'effectiveFrom', label: 'Effective From (ISO Date)', required: true, type: 'date', sampleValue: '2026-01-01', aliases: ['from', 'startdate', 'effective_from'] },
      { name: 'englishName', label: 'English Name', required: false, type: 'string', sampleValue: 'Net 30 Days', aliases: ['name', 'termname', 'name_en'] },
      { name: 'arabicName', label: 'Arabic Name', required: false, type: 'string', sampleValue: 'صافي 30 يوم', aliases: ['name_ar'] },
      { name: 'effectiveTo', label: 'Effective To (ISO Date)', required: false, type: 'date', sampleValue: '2026-12-31', aliases: ['to', 'enddate', 'effective_to'] },
      { name: 'baseDateRule', label: 'Base Date Rule', required: false, type: 'enum', sampleValue: 'DocumentDate', aliases: ['basedate', 'base_date_rule'] },
      { name: 'scheduleMode', label: 'Schedule Mode', required: false, type: 'enum', sampleValue: 'SingleDueDate', aliases: ['scheduletype', 'schedule_mode'] },
      { name: 'dueDays', label: 'Due Offset Days', required: false, type: 'number', sampleValue: '30', aliases: ['days', 'duedays_offset'] },
      { name: 'dueMonths', label: 'Due Offset Months', required: false, type: 'number', sampleValue: '0', aliases: ['months', 'duemonths_offset'] },
      { name: 'installments', label: 'Installments (JSON Array)', required: false, type: 'json', sampleValue: '[{"sequence":1,"percentage":100,"days":30,"months":0}]', aliases: ['installment_list'] },
      { name: 'earlySettlementDiscountEnabled', label: 'Early Discount Enabled', required: false, type: 'boolean', sampleValue: 'false', aliases: ['earlydiscount', 'early_discount_enabled'] },
      { name: 'earlySettlementDiscountPercentage', label: 'Early Discount %', required: false, type: 'number', sampleValue: '2.0', aliases: ['discountpercentage', 'discount_percent'] },
      { name: 'earlySettlementDiscountDays', label: 'Early Discount Days', required: false, type: 'number', sampleValue: '10', aliases: ['discountdays', 'early_discount_days'] },
      { name: 'earlySettlementDiscountMonths', label: 'Early Discount Months', required: false, type: 'number', sampleValue: '0', aliases: ['discountmonths', 'early_discount_months'] },
    ],
  },
  {
    kind: 'Tax',
    key: 'taxes',
    label: 'Tax / VAT',
    allowsMutableUpdate: false,
    fields: [
      { name: 'code', label: 'Tax Code', required: true, type: 'string', sampleValue: 'VAT-15', aliases: ['taxcode', 'ratecode'] },
      { name: 'categoryCode', label: 'Category Code', required: true, type: 'string', sampleValue: 'VAT-STANDARD', aliases: ['taxcategorycode', 'tax_category_code'] },
      { name: 'categoryNameEnglish', label: 'English Category Name', required: true, type: 'string', sampleValue: 'Standard VAT', aliases: ['categoryname', 'taxcategoryname'] },
      { name: 'categoryNameArabic', label: 'Arabic Category Name', required: false, type: 'string', sampleValue: 'ضريبة القيمة المضافة القياسية', aliases: ['categoryname_ar'] },
      { name: 'englishName', label: 'English Name', required: true, type: 'string', sampleValue: 'Standard 15% Rate', aliases: ['name', 'taxname', 'name_en'] },
      { name: 'arabicName', label: 'Arabic Name', required: false, type: 'string', sampleValue: 'نسبة 15% القياسية', aliases: ['name_ar'] },
      { name: 'applicability', label: 'Applicability (Purchase/Sales/Both)', required: false, type: 'enum', sampleValue: 'Both', aliases: ['direction', 'taxdirection', 'apply_to'] },
      { name: 'effectiveFrom', label: 'Effective From (ISO Date)', required: true, type: 'date', sampleValue: '2020-07-01', aliases: ['from', 'startdate', 'effective_from'] },
      { name: 'effectiveTo', label: 'Effective To (ISO Date)', required: false, type: 'date', sampleValue: '', aliases: ['to', 'enddate', 'effective_to'] },
      { name: 'ratePercentage', label: 'Rate Percentage', required: true, type: 'number', sampleValue: '15.00', aliases: ['rate', 'percentage', 'taxrate', 'vatrate'] },
    ],
  },
  {
    kind: 'ExchangeRate',
    key: 'exchange-rates',
    label: 'Exchange Rate',
    allowsMutableUpdate: false,
    fields: [
      { name: 'sourceCurrencyId', label: 'Source Currency ID', required: true, type: 'guid', sampleValue: 'd8e8a0e3-3333-4444-8888-cccccccccccc', aliases: ['sourcecurrency', 'source_currency_id', 'fromcurrencyid'] },
      { name: 'targetCurrencyId', label: 'Target Currency ID', required: true, type: 'guid', sampleValue: 'e9f9b1f4-4444-4444-8888-dddddddddddd', aliases: ['targetcurrency', 'target_currency_id', 'tocurrencyid'] },
      { name: 'effectiveFrom', label: 'Effective From (ISO Date)', required: true, type: 'date', sampleValue: '2026-01-01', aliases: ['from', 'date', 'effective_from'] },
      { name: 'effectiveTo', label: 'Effective To (ISO Date)', required: false, type: 'date', sampleValue: '', aliases: ['to', 'effective_to'] },
      { name: 'rate', label: 'Exchange Rate Value', required: true, type: 'number', sampleValue: '3.750000', aliases: ['fxrate', 'exchange_rate', 'value'] },
      { name: 'rateScale', label: 'Rate Scale Decimals', required: false, type: 'number', sampleValue: '6', aliases: ['scale', 'precision'] },
      { name: 'provenance', label: 'Provenance (Manual/Configured)', required: false, type: 'enum', sampleValue: 'Manual', aliases: ['source_type', 'provenance_type'] },
      { name: 'sourceNotes', label: 'Source Notes', required: false, type: 'string', sampleValue: 'SAMA official peg', aliases: ['notes', 'memo'] },
    ],
  },
  {
    kind: 'PriceList',
    key: 'price-lists',
    label: 'Price List',
    allowsMutableUpdate: true,
    fields: [
      { name: 'code', label: 'Price List Code', required: true, type: 'string', sampleValue: 'PL-STD-2026', aliases: ['pricelistcode', 'listcode'] },
      { name: 'englishName', label: 'English Name', required: true, type: 'string', sampleValue: 'Standard 2026 Wholesale', aliases: ['name', 'pricelistname', 'name_en'] },
      { name: 'currencyId', label: 'Currency ID', required: true, type: 'guid', sampleValue: 'd8e8a0e3-3333-4444-8888-cccccccccccc', aliases: ['currency', 'currency_id'] },
      { name: 'arabicName', label: 'Arabic Name', required: false, type: 'string', sampleValue: 'قائمة الجملة القياسية 2026', aliases: ['name_ar'] },
      { name: 'customerId', label: 'Specific Customer ID', required: false, type: 'guid', sampleValue: '', aliases: ['customer', 'customer_id'] },
      { name: 'organizationScopeKind', label: 'Org Scope Kind', required: false, type: 'enum', sampleValue: '', aliases: ['scopekind', 'scope_kind'] },
      { name: 'organizationScopeId', label: 'Org Scope ID', required: false, type: 'guid', sampleValue: '', aliases: ['scopeid', 'scope_id'] },
      { name: 'priority', label: 'Precedence Priority', required: false, type: 'number', sampleValue: '0', aliases: ['precedence', 'rank'] },
      { name: 'productId', label: 'Line Product ID', required: false, type: 'guid', sampleValue: '', aliases: ['product', 'product_id'] },
      { name: 'unitOfMeasureId', label: 'Line Unit of Measure ID', required: false, type: 'guid', sampleValue: '', aliases: ['uomid', 'unit_id'] },
      { name: 'effectiveFrom', label: 'Line Effective From', required: false, type: 'date', sampleValue: '2026-01-01', aliases: ['from', 'startdate'] },
      { name: 'effectiveTo', label: 'Line Effective To', required: false, type: 'date', sampleValue: '', aliases: ['to', 'enddate'] },
      { name: 'price', label: 'Unit Price Value', required: false, type: 'number', sampleValue: '125.50', aliases: ['unitprice', 'unit_price', 'amount'] },
      { name: 'priceScale', label: 'Price Scale Decimals', required: false, type: 'number', sampleValue: '2', aliases: ['scale'] },
      { name: 'provenance', label: 'Provenance', required: false, type: 'enum', sampleValue: 'Manual', aliases: ['provenance_type'] },
      { name: 'sourceReference', label: 'Source Reference', required: false, type: 'string', sampleValue: 'Annual review sheet', aliases: ['sourcereference', 'ref'] },
    ],
  },
] as const;

export function getImportResourceDefinition(
  kindOrKey: MasterDataImportResourceKind | string,
): ImportResourceDefinition | undefined {
  return IMPORT_RESOURCE_DEFINITIONS.find(
    (def) => def.kind === kindOrKey || def.key === kindOrKey,
  );
}
