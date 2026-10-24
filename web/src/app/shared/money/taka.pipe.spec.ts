import { TakaPipe } from './taka.pipe';

describe('TakaPipe', () => {
  const pipe = new TakaPipe();

  it('formats as Tk with Indian digit grouping', () => {
    expect(pipe.transform(2400)).toBe('Tk\u00a02,400');
    expect(pipe.transform(125000.5)).toBe('Tk\u00a01,25,000.5');
  });

  it('renders nothing for missing values', () => {
    expect(pipe.transform(null)).toBe('');
  });
});
