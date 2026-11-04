import { Directive, ElementRef, HostListener, inject } from '@angular/core';

/** Same-origin placeholder (allowed by the CSP `img-src 'self'`). */
const PLACEHOLDER_SRC = '/assets/photo-placeholder.svg';

/**
 * Replaces a photo that fails to load with a neutral placeholder, instead of the browser's
 * broken-image icon. Swaps once: if the placeholder itself fails, nothing further happens.
 */
@Directive({
  selector: 'img[hbImageFallback]',
  standalone: true,
})
export class ImageFallbackDirective {
  private readonly image = inject<ElementRef<HTMLImageElement>>(ElementRef).nativeElement;

  @HostListener('error')
  onError(): void {
    if (this.image.getAttribute('src') !== PLACEHOLDER_SRC) {
      this.image.src = PLACEHOLDER_SRC;
    }
  }
}
