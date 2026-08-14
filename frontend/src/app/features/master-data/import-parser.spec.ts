import { describe, expect, it } from 'vitest';
import {
  autoMatchColumns,
  buildNormalizedRows,
  parseCsvContent,
  parseImportFileContent,
  parseJsonContent,
  validateColumnMappings,
} from './import-parser';
import { MasterDataImportResourceKind } from './master-data-import.models';

describe('ImportParser - CSV Support', () => {
  it('parses normal header and rows with stable 1-indexed row numbers', () => {
    const csv = 'code,englishName,arabicName\nCAT-01,Beverages,مشروبات\nCAT-02,Food,أغذية';
    const result = parseCsvContent(csv);

    expect(result.valid).toBe(true);
    expect(result.format).toBe('csv');
    expect(result.headers).toEqual(['code', 'englishName', 'arabicName']);
    expect(result.totalRows).toBe(2);
    expect(result.rows).toHaveLength(2);
    expect(result.rows[0].rowNumber).toBe(1);
    expect(result.rows[0].rawFields).toEqual({
      code: 'CAT-01',
      englishName: 'Beverages',
      arabicName: 'مشروبات',
    });
    expect(result.rows[1].rowNumber).toBe(2);
    expect(result.rows[1].rawFields).toEqual({
      code: 'CAT-02',
      englishName: 'Food',
      arabicName: 'أغذية',
    });
  });

  it('handles delimiters inside quoted fields correctly', () => {
    const csv = 'sku,englishName,description\nSKU-1,"Valve, High Pressure","Heavy, durable valve"';
    const result = parseCsvContent(csv);

    expect(result.valid).toBe(true);
    expect(result.rows[0].rawFields['englishName']).toBe('Valve, High Pressure');
    expect(result.rows[0].rawFields['description']).toBe('Heavy, durable valve');
  });

  it('handles escaped quotes inside quoted fields', () => {
    const csv = 'code,englishName\nTERM-01,"Net 30 ""Special"" Terms"';
    const result = parseCsvContent(csv);

    expect(result.valid).toBe(true);
    expect(result.rows[0].rawFields['englishName']).toBe('Net 30 "Special" Terms');
  });

  it('handles embedded line breaks inside quoted fields', () => {
    const csv = 'sku,description\nSKU-001,"Line 1\nLine 2\nLine 3"';
    const result = parseCsvContent(csv);

    expect(result.valid).toBe(true);
    expect(result.rows[0].rawFields['description']).toBe('Line 1\nLine 2\nLine 3');
  });

  it('handles UTF-8 with BOM prefix smoothly', () => {
    const csv = '\uFEFFcode,englishName\nPCS,Pieces';
    const result = parseCsvContent(csv);

    expect(result.valid).toBe(true);
    expect(result.headers[0]).toBe('code');
    expect(result.rows[0].rawFields['code']).toBe('PCS');
  });

  it('treats empty fields as null', () => {
    const csv = 'code,englishName,arabicName\nCAT-01,Beverages,';
    const result = parseCsvContent(csv);

    expect(result.valid).toBe(true);
    expect(result.rows[0].rawFields['arabicName']).toBeNull();
  });

  it('reports safe structural error on unclosed quotation mark', () => {
    const csv = 'code,name\nCAT-01,"Unclosed field here';
    const result = parseCsvContent(csv);

    expect(result.valid).toBe(false);
    expect(result.error).toContain('Unclosed quotation mark');
  });

  it('reports safe error on empty input', () => {
    const result = parseCsvContent('   \n  \r\n ');
    expect(result.valid).toBe(false);
    expect(result.error).toContain('empty');
  });

  it('enforces maximum row guard', () => {
    const csv = 'code,name\nC1,N1\nC2,N2\nC3,N3';
    const result = parseCsvContent(csv, 2);

    expect(result.valid).toBe(false);
    expect(result.error).toContain('exceeds the maximum allowed 2 rows');
  });
});

describe('ImportParser - JSON Support', () => {
  it('parses valid JSON array of row objects with stable row numbers', () => {
    const json = JSON.stringify([
      { sku: 'SKU-100', englishName: 'Electric Motor', categoryId: 'cat-guid-1' },
      { sku: 'SKU-200', englishName: 'Diesel Generator', categoryId: 'cat-guid-2' },
    ]);
    const result = parseJsonContent(json);

    expect(result.valid).toBe(true);
    expect(result.format).toBe('json');
    expect(result.headers).toEqual(
      expect.arrayContaining(['sku', 'englishName', 'categoryId']),
    );
    expect(result.rows).toHaveLength(2);
    expect(result.rows[0].rowNumber).toBe(1);
    expect(result.rows[0].rawFields['sku']).toBe('SKU-100');
    expect(result.rows[1].rowNumber).toBe(2);
    expect(result.rows[1].rawFields['sku']).toBe('SKU-200');
  });

  it('parses JSON structure with { rows: [...] }', () => {
    const json = JSON.stringify({
      rows: [
        { code: 'SAR', englishName: 'Saudi Riyal', arabicName: 'ريال سعودي' },
      ],
    });
    const result = parseJsonContent(json);

    expect(result.valid).toBe(true);
    expect(result.rows[0].rawFields['arabicName']).toBe('ريال سعودي');
  });

  it('reports safe structural error on malformed JSON without crashing', () => {
    const json = '{ "broken json: [';
    const result = parseJsonContent(json);

    expect(result.valid).toBe(false);
    expect(result.error).toContain('Malformed JSON');
  });

  it('reports error on unsupported root structure', () => {
    const json = JSON.stringify('plain string');
    const result = parseJsonContent(json);

    expect(result.valid).toBe(false);
    expect(result.error).toContain('Invalid JSON structure');
  });

  it('handles parseImportFileContent auto-detection for CSV and JSON', () => {
    const csvRes = parseImportFileContent('code,name\n1,A', 'data.csv');
    expect(csvRes.format).toBe('csv');

    const jsonRes = parseImportFileContent('[{"code":"1"}]', 'data.json');
    expect(jsonRes.format).toBe('json');
  });
});

describe('ImportParser - Column Mapping & Normalization Seam', () => {
  const allTenResources: MasterDataImportResourceKind[] = [
    'ProductCategory',
    'UnitOfMeasure',
    'Product',
    'Supplier',
    'BusinessCustomer',
    'Currency',
    'PaymentTerm',
    'Tax',
    'ExchangeRate',
    'PriceList',
  ];

  it('supports auto-matching across all 10 resource kinds', () => {
    for (const resourceKind of allTenResources) {
      const headers = ['code', 'name', 'sku', 'legalname', 'rate', 'price', 'effectivefrom'];
      const mappings = autoMatchColumns(headers, resourceKind);
      expect(mappings).toBeDefined();
      expect(mappings.length).toBe(headers.length);
    }
  });

  it('accurately matches aliases like "tax_category_code" and "vat_number"', () => {
    const taxMappings = autoMatchColumns(['tax_category_code', 'rate_percentage'], 'Tax');
    expect(taxMappings.find((m) => m.sourceColumn === 'tax_category_code')?.targetField).toBe(
      'categoryCode',
    );
    expect(taxMappings.find((m) => m.sourceColumn === 'rate_percentage')?.targetField).toBe(
      'ratePercentage',
    );

    const supMappings = autoMatchColumns(['vat_number', 'vendor_code'], 'Supplier');
    expect(supMappings.find((m) => m.sourceColumn === 'vat_number')?.targetField).toBe(
      'registrationReference',
    );
    expect(supMappings.find((m) => m.sourceColumn === 'vendor_code')?.targetField).toBe('code');
  });

  it('validates required fields and reports missing mappings', () => {
    // Product requires sku, englishName, categoryId, baseUnitOfMeasureId
    const incompleteMappings = [
      { sourceColumn: 'sku', targetField: 'sku' },
      { sourceColumn: 'englishName', targetField: 'englishName' },
    ];
    const validation = validateColumnMappings(incompleteMappings, 'Product');

    expect(validation.valid).toBe(false);
    expect(validation.missingRequiredFields).toContain('categoryId');
    expect(validation.missingRequiredFields).toContain('baseUnitOfMeasureId');
    expect(validation.errors.length).toBeGreaterThanOrEqual(2);
  });

  it('detects duplicate target field mappings', () => {
    const duplicateMappings = [
      { sourceColumn: 'Header_A', targetField: 'code' },
      { sourceColumn: 'Header_B', targetField: 'code' },
      { sourceColumn: 'Header_C', targetField: 'englishName' },
    ];
    const validation = validateColumnMappings(duplicateMappings, 'ProductCategory');

    expect(validation.valid).toBe(false);
    expect(validation.duplicateMappings).toContain('code');
    expect(validation.errors[0]).toContain("Target field 'code' is mapped to multiple");
  });

  it('builds normalized rows ready for backend submission with lowercase keys and preserved row numbers', () => {
    const parsedRows = [
      {
        rowNumber: 1,
        rawFields: {
          'Col SKU': 'SKU-001',
          'Col Name': 'Widget Alpha',
          'Col Cat': 'guid-cat-1',
          'Col Unit': 'guid-unit-1',
        },
      },
      {
        rowNumber: 2,
        rawFields: {
          'Col SKU': 'SKU-002',
          'Col Name': 'Widget Beta',
          'Col Cat': 'guid-cat-1',
          'Col Unit': 'guid-unit-1',
        },
      },
    ];

    const mappings = [
      { sourceColumn: 'Col SKU', targetField: 'sku' },
      { sourceColumn: 'Col Name', targetField: 'englishName' },
      { sourceColumn: 'Col Cat', targetField: 'categoryId' },
      { sourceColumn: 'Col Unit', targetField: 'baseUnitOfMeasureId' },
    ];

    const normalized = buildNormalizedRows(parsedRows, mappings);

    expect(normalized).toHaveLength(2);
    expect(normalized[0].rowNumber).toBe(1);
    expect(normalized[0].fields).toEqual({
      sku: 'SKU-001',
      englishname: 'Widget Alpha',
      categoryid: 'guid-cat-1',
      baseunitofmeasureid: 'guid-unit-1',
    });
    expect(normalized[1].rowNumber).toBe(2);
    expect(normalized[1].fields).toEqual({
      sku: 'SKU-002',
      englishname: 'Widget Beta',
      categoryid: 'guid-cat-1',
      baseunitofmeasureid: 'guid-unit-1',
    });
  });
});
