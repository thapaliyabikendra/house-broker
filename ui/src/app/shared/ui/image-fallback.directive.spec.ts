import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ImageFallbackDirective } from './image-fallback.directive';

@Component({
  imports: [ImageFallbackDirective],
  template: `<img hbImageFallback [src]="src" alt="" />`,
})
class HostComponent {
  src = '/api/v1/listings/a1/images/i1';
}

describe('ImageFallbackDirective', () => {
  function setup() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    const img = host.querySelector('img') as HTMLImageElement;
    return { img, fixture };
  }

  it('swaps a failed photo for the placeholder', () => {
    const { img } = setup();

    img.dispatchEvent(new Event('error'));

    expect(img.getAttribute('src')).toBe('/assets/photo-placeholder.svg');
  });

  it('does not loop when the placeholder itself fails', () => {
    const { img } = setup();

    img.dispatchEvent(new Event('error'));
    img.dispatchEvent(new Event('error'));

    expect(img.getAttribute('src')).toBe('/assets/photo-placeholder.svg');
  });
});
