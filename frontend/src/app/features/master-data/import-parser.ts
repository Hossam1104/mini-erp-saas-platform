import {
  ImportColumnMapping,
  ImportMappingValidation,
  ImportParseResult,
  ImportParsedRow,
  MasterDataImportResourceKind,
  MasterDataImportRowInput,
  getImportResourceDefinition,
} from './master-data-import.models';

export const MAX_IMPORT_ROWS = 10000;
export const MAX_FILE_SIZE_BYTES = 10 * 1024 * 1024; // 10 MB UX protection

/**
 * Parses raw CSV content adhering to RFC 4180:
 * - Handles UTF-8 and BOM (\uFEFF)
 * - Quoted fields with commas, newlines, and escaped quotes ("")
 * - Preserves Arabic Unicode characters
 * - Generates stable 1-indexed source row numbers
 */
export function parseCsvContent(
  rawContent: string,
  maxRows: number = MAX_IMPORT_ROWS,
): ImportParseResult {
  if (typeof rawContent !== 'string' || rawContent.length === 0) {
    return {
      valid: false,
      format: 'csv',
      headers: [],
      rows: [],
      totalRows: 0,
      error: 'The CSV file is empty.',
    };
  }

  // Strip BOM if present
  const content = rawContent.charCodeAt(0) === 0xfeff ? rawContent.slice(1) : rawContent;
  if (content.trim().length === 0) {
    return {
      valid: false,
      format: 'csv',
      headers: [],
      rows: [],
      totalRows: 0,
      error: 'The CSV file is empty.',
    };
  }

  const rawRows: string[][] = [];
  let currentRow: string[] = [];
  let currentField = '';
  let inQuotes = false;
  let i = 0;
  const len = content.length;

  while (i < len) {
    const char = content[i];

    if (inQuotes) {
      if (char === '"') {
        if (i + 1 < len && content[i + 1] === '"') {
          // Escaped quote
          currentField += '"';
          i += 2;
          continue;
        } else {
          // Closing quote
          inQuotes = false;
          i++;
          continue;
        }
      } else {
        currentField += char;
        i++;
        continue;
      }
    } else {
      if (char === '"') {
        inQuotes = true;
        i++;
        continue;
      } else if (char === ',') {
        currentRow.push(currentField.trim());
        currentField = '';
        i++;
        continue;
      } else if (char === '\r') {
        if (i + 1 < len && content[i + 1] === '\n') {
          i++;
        }
        currentRow.push(currentField.trim());
        currentField = '';
        if (currentRow.some((f) => f.length > 0)) {
          rawRows.push(currentRow);
        }
        currentRow = [];
        i++;
        continue;
      } else if (char === '\n') {
        currentRow.push(currentField.trim());
        currentField = '';
        if (currentRow.some((f) => f.length > 0)) {
          rawRows.push(currentRow);
        }
        currentRow = [];
        i++;
        continue;
      } else {
        currentField += char;
        i++;
        continue;
      }
    }
  }

  if (inQuotes) {
    return {
      valid: false,
      format: 'csv',
      headers: [],
      rows: [],
      totalRows: 0,
      error: 'Malformed CSV: Unclosed quotation mark detected in the input.',
    };
  }

  // Push final field/row if not empty
  currentRow.push(currentField.trim());
  if (currentRow.some((f) => f.length > 0)) {
    rawRows.push(currentRow);
  }

  if (rawRows.length === 0) {
    return {
      valid: false,
      format: 'csv',
      headers: [],
      rows: [],
      totalRows: 0,
      error: 'The CSV file contains no rows.',
    };
  }

  const rawHeaders = rawRows[0];
  const headers = rawHeaders.map((h, idx) => (h.length > 0 ? h : `Column_${idx + 1}`));

  if (headers.length === 0 || headers.every((h) => h.length === 0)) {
    return {
      valid: false,
      format: 'csv',
      headers: [],
      rows: [],
      totalRows: 0,
      error: 'The CSV header row is empty.',
    };
  }

  const dataRawRows = rawRows.slice(1);
  if (dataRawRows.length > maxRows) {
    return {
      valid: false,
      format: 'csv',
      headers,
      rows: [],
      totalRows: dataRawRows.length,
      error: `The file exceeds the maximum allowed ${maxRows} rows (found ${dataRawRows.length}).`,
    };
  }

  const parsedRows: ImportParsedRow[] = [];
  for (let r = 0; r < dataRawRows.length; r++) {
    const rowData = dataRawRows[r];
    const rawFields: Record<string, string | null> = {};
    for (let c = 0; c < headers.length; c++) {
      const header = headers[c];
      const val = c < rowData.length ? rowData[c] : '';
      rawFields[header] = val.length > 0 ? val : null;
    }
    parsedRows.push({
      rowNumber: r + 1, // Stable 1-indexed source row counter
      rawFields,
    });
  }

  return {
    valid: true,
    format: 'csv',
    headers,
    rows: parsedRows,
    totalRows: parsedRows.length,
    error: null,
  };
}

/**
 * Parses raw JSON content:
 * - Supports an array of row objects: [ { "sku": "1", ... }, ... ]
 * - Supports structured object: { "rows": [ ... ] }
 * - Preserves Arabic Unicode characters
 * - Generates stable 1-indexed source row numbers
 */
export function parseJsonContent(
  rawContent: string,
  maxRows: number = MAX_IMPORT_ROWS,
): ImportParseResult {
  if (typeof rawContent !== 'string' || rawContent.trim().length === 0) {
    return {
      valid: false,
      format: 'json',
      headers: [],
      rows: [],
      totalRows: 0,
      error: 'The JSON content is empty.',
    };
  }

  let parsed: unknown;
  try {
    parsed = JSON.parse(rawContent);
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Invalid JSON syntax';
    return {
      valid: false,
      format: 'json',
      headers: [],
      rows: [],
      totalRows: 0,
      error: `Malformed JSON: ${message}`,
    };
  }

  let rawList: unknown[] = [];
  if (Array.isArray(parsed)) {
    rawList = parsed;
  } else if (
    parsed !== null &&
    typeof parsed === 'object' &&
    'rows' in parsed &&
    Array.isArray((parsed as { rows: unknown[] }).rows)
  ) {
    rawList = (parsed as { rows: unknown[] }).rows;
  } else {
    return {
      valid: false,
      format: 'json',
      headers: [],
      rows: [],
      totalRows: 0,
      error: 'Invalid JSON structure: Expected a JSON array of row objects or { rows: [...] }.',
    };
  }

  if (rawList.length > maxRows) {
    return {
      valid: false,
      format: 'json',
      headers: [],
      rows: [],
      totalRows: rawList.length,
      error: `The input exceeds the maximum allowed ${maxRows} rows (found ${rawList.length}).`,
    };
  }

  // Extract all distinct keys across row objects for header discovery
  const headerSet = new Set<string>();
  const parsedRows: ImportParsedRow[] = [];

  for (let i = 0; i < rawList.length; i++) {
    const item = rawList[i];
    if (item === null || typeof item !== 'object' || Array.isArray(item)) {
      return {
        valid: false,
        format: 'json',
        headers: [],
        rows: [],
        totalRows: 0,
        error: `Row ${i + 1} is not a valid JSON object.`,
      };
    }

    // Check if row has nested fields format { rowNumber: 1, fields: { ... } }
    const sourceObj =
      'fields' in item && typeof (item as { fields: unknown }).fields === 'object' && (item as { fields: unknown }).fields !== null
        ? ((item as { fields: Record<string, unknown> }).fields as Record<string, unknown>)
        : (item as Record<string, unknown>);

    const rawFields: Record<string, string | null> = {};
    for (const [k, v] of Object.entries(sourceObj)) {
      headerSet.add(k);
      if (v === null || v === undefined) {
        rawFields[k] = null;
      } else if (typeof v === 'object') {
        rawFields[k] = JSON.stringify(v);
      } else {
        const str = String(v).trim();
        rawFields[k] = str.length > 0 ? str : null;
      }
    }

    parsedRows.push({
      rowNumber: i + 1, // Stable 1-indexed source row counter
      rawFields,
    });
  }

  const headers = Array.from(headerSet);

  return {
    valid: true,
    format: 'json',
    headers,
    rows: parsedRows,
    totalRows: parsedRows.length,
    error: null,
  };
}

/**
 * Universal entry point to parse a file or content string based on format / filename.
 */
export function parseImportFileContent(
  rawContent: string,
  fileName: string,
  maxRows: number = MAX_IMPORT_ROWS,
): ImportParseResult {
  const isJson = fileName.toLowerCase().endsWith('.json') || rawContent.trim().startsWith('{') || rawContent.trim().startsWith('[');
  return isJson ? parseJsonContent(rawContent, maxRows) : parseCsvContent(rawContent, maxRows);
}

// ---------------------------------------------------------------------------
// Column Mapping & Normalization Seam
// ---------------------------------------------------------------------------

function normalizeKey(str: string): string {
  return str.toLowerCase().replace(/[^a-z0-9]/g, '');
}

/**
 * Automatically produces initial column mappings by matching source headers against
 * the target resource definition's field names and aliases.
 */
export function autoMatchColumns(
  sourceHeaders: string[],
  resourceKind: MasterDataImportResourceKind,
): ImportColumnMapping[] {
  const definition = getImportResourceDefinition(resourceKind);
  if (!definition) {
    return sourceHeaders.map((h) => ({ sourceColumn: h, targetField: null }));
  }

  const targetFields = definition.fields;
  const usedTargets = new Set<string>();

  return sourceHeaders.map((header) => {
    const normHeader = normalizeKey(header);

    // 1. Direct name match
    let matched = targetFields.find(
      (f) => normalizeKey(f.name) === normHeader && !usedTargets.has(f.name),
    );

    // 2. Alias match
    if (!matched) {
      matched = targetFields.find(
        (f) =>
          !usedTargets.has(f.name) &&
          f.aliases?.some((a) => normalizeKey(a) === normHeader),
      );
    }

    // 3. Label match
    if (!matched) {
      matched = targetFields.find(
        (f) => !usedTargets.has(f.name) && normalizeKey(f.label) === normHeader,
      );
    }

    if (matched) {
      usedTargets.add(matched.name);
      return { sourceColumn: header, targetField: matched.name };
    }

    return { sourceColumn: header, targetField: null };
  });
}

/**
 * Validates column mappings for required fields and duplicate assignments.
 */
export function validateColumnMappings(
  mappings: ImportColumnMapping[],
  resourceKind: MasterDataImportResourceKind,
): ImportMappingValidation {
  const definition = getImportResourceDefinition(resourceKind);
  if (!definition) {
    return {
      valid: false,
      errors: [`Unsupported resource kind: ${resourceKind}`],
      missingRequiredFields: [],
      duplicateMappings: [],
      mappedFieldsCount: 0,
    };
  }

  const errors: string[] = [];
  const mappedTargets: string[] = [];
  const targetCounts = new Map<string, number>();

  for (const mapping of mappings) {
    if (mapping.targetField) {
      mappedTargets.push(mapping.targetField);
      const current = targetCounts.get(mapping.targetField) ?? 0;
      targetCounts.set(mapping.targetField, current + 1);
    }
  }

  // Check duplicate target mappings
  const duplicateMappings: string[] = [];
  for (const [target, count] of targetCounts.entries()) {
    if (count > 1) {
      duplicateMappings.push(target);
      errors.push(`Target field '${target}' is mapped to multiple source columns.`);
    }
  }

  // Check required fields
  const missingRequiredFields: string[] = [];
  for (const field of definition.fields) {
    if (field.required && !mappedTargets.includes(field.name)) {
      missingRequiredFields.push(field.name);
      errors.push(`Required field '${field.label}' (${field.name}) is not mapped.`);
    }
  }

  // Special name validation: For Category, UOM, Currency, PaymentTerm, at least one of englishName or arabicName is needed
  if (['ProductCategory', 'UnitOfMeasure', 'Currency', 'PaymentTerm'].includes(resourceKind)) {
    const hasEnglish = mappedTargets.includes('englishName');
    const hasArabic = mappedTargets.includes('arabicName');
    if (!hasEnglish && !hasArabic) {
      errors.push('At least one name column (English Name or Arabic Name) must be mapped.');
    }
  }

  return {
    valid: errors.length === 0,
    errors,
    missingRequiredFields,
    duplicateMappings,
    mappedFieldsCount: mappedTargets.length,
  };
}

/**
 * Transforms parsed raw rows into normalized row inputs matching backend contract.
 * Preserves 1-indexed original source row numbering.
 */
export function buildNormalizedRows(
  parsedRows: ImportParsedRow[],
  mappings: ImportColumnMapping[],
): MasterDataImportRowInput[] {
  const activeMappings = mappings.filter((m) => m.targetField !== null) as Array<{
    sourceColumn: string;
    targetField: string;
  }>;

  return parsedRows.map((row) => {
    const fields: Record<string, string | null> = {};
    for (const { sourceColumn, targetField } of activeMappings) {
      const value = row.rawFields[sourceColumn];
      // Normalize key name (backend expects lower-case or exact match)
      fields[targetField.toLowerCase()] = value !== undefined && value !== null ? value.trim() : null;
    }

    return {
      rowNumber: row.rowNumber,
      fields,
    };
  });
}
