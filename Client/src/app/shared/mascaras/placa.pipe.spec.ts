import { PlacaPipe } from './placa.pipe';

describe('PlacaPipe — placa no mesmo formato em todas as telas', () => {
  const pipe = new PlacaPipe();

  it('coloca hífen e maiúsculas nas placas dos dois padrões', () => {
    expect(pipe.transform('abc1d23')).toBe('ABC-1D23');
    expect(pipe.transform('ABC1234')).toBe('ABC-1234');
  });

  it('mantém o que já está formatado e não quebra com valor vazio', () => {
    expect(pipe.transform('ABC-1D23')).toBe('ABC-1D23');
    expect(pipe.transform(null)).toBe('');
    expect(pipe.transform(undefined)).toBe('');
    expect(pipe.transform('')).toBe('');
  });
});
