import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type BrandVariant = 'icon' | 'logo' | 'compact';
export type BrandTheme = 'auto' | 'light' | 'dark';

const BRAND_ASSETS: Record<BrandVariant, { light: string; dark: string; width: number; height: number }> = {
  icon: {
    // The sidebar uses small transparent derivatives generated from the Owner
    // icon sources; full artwork remains the source of truth for larger marks.
    light: 'assets/brand/favicon-64.png',
    dark: 'assets/brand/favicon-dark-64.png',
    width: 64,
    height: 64,
  },
  logo: {
    light: 'assets/Logo_16_9_BG_Removed.png',
    dark: 'assets/Logo_16_9_BG_Removed_Dark.png',
    width: 1536,
    height: 1024,
  },
  compact: {
    light: 'assets/Logo_4_3_BG_Removed.png',
    dark: 'assets/Logo_4_3_BG_Removed_Dark.png',
    width: 1254,
    height: 1254,
  },
};

/**
 * Renders the Owner-provided transparent brand artwork. The artwork is never
 * mirrored, stretched, cropped, or placed on an artificial surface; the dark
 * variant is used only against dark surfaces.
 */
@Component({
  selector: 'app-brand-mark',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (theme() === 'auto') {
      <picture class="brand-mark__picture">
        <source [attr.srcset]="assets().dark" media="(prefers-color-scheme: dark)" />
        <img class="brand-mark__image" [src]="assets().light" [attr.width]="assets().width" [attr.height]="assets().height" [alt]="alt()" />
      </picture>
    } @else {
      <img class="brand-mark__image" [src]="theme() === 'dark' ? assets().dark : assets().light" [attr.width]="assets().width" [attr.height]="assets().height" [alt]="alt()" />
    }
  `,
  styles: `
    :host { display: block; }
    .brand-mark__picture { display: block; }
    .brand-mark__image { display: block; width: 100%; height: auto; object-fit: contain; transform: none; }
  `,
})
export class BrandMarkComponent {
  readonly variant = input<BrandVariant>('icon');
  readonly theme = input<BrandTheme>('auto');
  readonly alt = input('');

  protected readonly assets = computed(() => BRAND_ASSETS[this.variant()]);
}
