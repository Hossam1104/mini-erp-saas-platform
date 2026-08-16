import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BrandMarkComponent } from './brand-mark.component';

describe('BrandMarkComponent', () => {
  let fixture: ComponentFixture<BrandMarkComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [BrandMarkComponent] }).compileComponents();
  });

  function create(variant: 'icon' | 'logo' | 'compact', theme: 'auto' | 'light' | 'dark'): HTMLElement {
    fixture = TestBed.createComponent(BrandMarkComponent);
    fixture.componentRef.setInput('variant', variant);
    fixture.componentRef.setInput('theme', theme);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders the transparent owner icon for the icon variant', () => {
    const element = create('icon', 'light');
    const img = element.querySelector('img.brand-mark__image') as HTMLImageElement;
    expect(img.getAttribute('src')).toBe('assets/brand/favicon-64.png');
    expect(img.getAttribute('width')).toBe('64');
    expect(img.getAttribute('height')).toBe('64');
    expect(img.getAttribute('alt')).toBe('');
  });

  it('renders the 16:9 logo variant', () => {
    const element = create('logo', 'light');
    const img = element.querySelector('img.brand-mark__image') as HTMLImageElement;
    expect(img.getAttribute('src')).toBe('assets/Logo_16_9_BG_Removed.png');
    expect(img.getAttribute('width')).toBe('1536');
    expect(img.getAttribute('height')).toBe('1024');
  });

  it('renders the 4:3 compact variant', () => {
    const element = create('compact', 'light');
    const img = element.querySelector('img.brand-mark__image') as HTMLImageElement;
    expect(img.getAttribute('src')).toBe('assets/Logo_4_3_BG_Removed.png');
  });

  it('uses the owner dark asset against dark surfaces', () => {
    const element = create('icon', 'dark');
    const img = element.querySelector('img.brand-mark__image') as HTMLImageElement;
    expect(img.getAttribute('src')).toBe('assets/brand/favicon-dark-64.png');
  });

  it('switches between light and dark assets in auto theme via prefers-color-scheme', () => {
    const element = create('compact', 'auto');
    const source = element.querySelector('picture source');
    const img = element.querySelector('img.brand-mark__image') as HTMLImageElement;
    expect(source?.getAttribute('srcset')).toBe('assets/Logo_4_3_BG_Removed_Dark.png');
    expect(source?.getAttribute('media')).toBe('(prefers-color-scheme: dark)');
    expect(img.getAttribute('src')).toBe('assets/Logo_4_3_BG_Removed.png');
    expect(img.getAttribute('width')).toBe('1254');
    expect(img.getAttribute('height')).toBe('1254');
  });

  it('never references the obsolete white-tile sidebar asset', () => {
    for (const variant of ['icon', 'logo', 'compact'] as const) {
      const element = create(variant, 'dark');
      expect(element.innerHTML).not.toContain('assets/brand/icon-96.png');
    }
  });

  it('does not mirror the artwork in any direction (RTL-safe)', () => {
    const element = create('icon', 'light');
    const img = element.querySelector('img.brand-mark__image') as HTMLImageElement;
    expect(img.style.transform).toBe('');
    expect(element.querySelector('[dir]')).toBeNull();
  });
});
