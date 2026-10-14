import { FormControl, FormGroup } from '@angular/forms';
import { distribuirErrosNosCampos } from './erros-de-campo';

describe('distribuirErrosNosCampos — mensagens da API no campo certo', () => {
  const formulario = () =>
    new FormGroup({
      nome: new FormControl(''),
      cpf: new FormControl(''),
      email: new FormControl(''),
      automovelId: new FormControl(''),
      condutorId: new FormControl('')
    });

  it('liga a mensagem ao campo cujo nome aparece nela (sem acento e com "e-mail")', () => {
    const form = formulario();

    const restantes = distribuirErrosNosCampos(form, [
      'Já existe um cliente com este CPF.',
      'Este e-mail já está em uso.',
      'O automóvel já está reservado para este período.'
    ]);

    expect(restantes).toEqual([]);
    expect(form.controls.cpf.errors).toEqual({ servidor: 'Já existe um cliente com este CPF.' });
    expect(form.controls.email.errors).toEqual({ servidor: 'Este e-mail já está em uso.' });
    expect(form.controls.automovelId.errors).toEqual({ servidor: 'O automóvel já está reservado para este período.' });
    expect(form.controls.cpf.touched).toBeTrue();
  });

  it('mensagem que cita dois campos ou nenhum fica no alerta geral', () => {
    const form = formulario();

    const restantes = distribuirErrosNosCampos(form, [
      'O condutor não pertence ao automóvel informado.',
      'Erro inesperado no servidor.'
    ]);

    expect(restantes).toEqual(['O condutor não pertence ao automóvel informado.', 'Erro inesperado no servidor.']);
    expect(form.controls.condutorId.errors).toBeNull();
    expect(form.controls.automovelId.errors).toBeNull();
  });

  it('um campo recebe só uma mensagem; a segunda vai para o alerta', () => {
    const form = formulario();

    const restantes = distribuirErrosNosCampos(form, ['CPF inválido.', 'O CPF já existe.']);

    expect(form.controls.cpf.errors).toEqual({ servidor: 'CPF inválido.' });
    expect(restantes).toEqual(['O CPF já existe.']);
  });

  it('apelidos ligam palavras diferentes do nome do campo', () => {
    const form = formulario();

    const restantes = distribuirErrosNosCampos(form, ['A CNH está vencida.'], { nome: ['cnh'] });

    expect(restantes).toEqual([]);
    expect(form.controls.nome.errors).toEqual({ servidor: 'A CNH está vencida.' });
  });

  it('o erro do servidor some quando o valor do campo muda', () => {
    const form = formulario();
    distribuirErrosNosCampos(form, ['CPF já cadastrado.']);

    form.controls.cpf.setValue('123');

    expect(form.controls.cpf.errors).toBeNull();
  });
});
