import { Pipe, PipeTransform } from '@angular/core';

const format = new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 });

/**
 * Formats BDT as shoppers in Bangladesh read it: "Tk 2,400" with lakh grouping. "Tk" rather than ৳,
 * because the display fonts have no taka glyph and the fallback looks out of place.
 */
@Pipe({ name: 'taka' })
export class TakaPipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    return value == null ? '' : `Tk\u00a0${format.format(value)}`;
  }
}
