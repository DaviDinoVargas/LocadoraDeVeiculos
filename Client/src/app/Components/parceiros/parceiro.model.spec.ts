import { categoriaLabel } from './parceiro.model';

describe('categoriaLabel', () => {
  it('traduz uma categoria conhecida para o rótulo em português', () => {
    expect(categoriaLabel('PostoDeCombustivel')).toBe('Posto de Combustível');
    expect(categoriaLabel('Hotel')).toBe('Hotel');
  });

  it('devolve o valor original quando a categoria não é reconhecida', () => {
    // Evita que a tela quebre silenciosamente se o backend mandar um
    // valor de enum novo que o Angular ainda não conhece.
    expect(categoriaLabel('CategoriaInexistente')).toBe('CategoriaInexistente');
  });
});
