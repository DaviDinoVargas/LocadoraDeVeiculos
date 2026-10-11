import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { ConfirmacaoService } from '../core/confirmacao.service';
import { ClientesListComponent } from './clientes/clientes-list.component';
import { ClientesService } from './clientes/clientes.service';
import { CondutoresListComponent } from './condutor/condutores-list.component';
import { CondutoresService } from './condutor/condutores.service';
import { CuponsListComponent } from './cupons/cupons-list.component';
import { CuponsService } from './cupons/cupons.service';
import { DesafiosCupomListComponent } from './desafios-cupom/desafios-cupom-list.component';
import { DesafiosCupomService } from './desafios-cupom/desafios-cupom.service';
import { DevolucoesListComponent } from './devolucoes/devolucoes-list.component';
import { DevolucoesService } from './devolucoes/devolucoes.service';
import { FuncionariosListComponent } from './funcionarios/funcionarios-list.component';
import { FuncionariosService } from './funcionarios/funcionarios.service';
import { GruposVeiculoListComponent } from './grupoveiculos/grupos-veiculo-list.component';
import { GruposVeiculoService } from './grupoveiculos/grupos-veiculo.service';
import { ParceirosListComponent } from './parceiros/parceiros-list.component';
import { ParceirosService } from './parceiros/parceiros.service';
import { PlanosCobrancaListComponent } from './planocobranca/planos-cobranca-list.component';
import { PlanosCobrancaService } from './planocobranca/planos-cobranca.service';
import { TaxasServicosListComponent } from './taxa-servico/taxas-servicos-list.component';
import { TaxasServicosService } from './taxa-servico/taxas-servicos.service';

// O confirm() nativo (bloqueante, sem estilo, sem acessibilidade) foi trocado por um diálogo em todas as
// telas de exclusão. A garantia que importa: cancelar NÃO exclui, confirmar exclui.
describe('Exclusão nas listas — confirmação por diálogo', () => {
  const casos: { nome: string; componente: Type<unknown>; servico: Type<unknown> }[] = [
    { nome: 'Clientes', componente: ClientesListComponent, servico: ClientesService },
    { nome: 'Condutores', componente: CondutoresListComponent, servico: CondutoresService },
    { nome: 'Cupons', componente: CuponsListComponent, servico: CuponsService },
    { nome: 'Desafios de cupom', componente: DesafiosCupomListComponent, servico: DesafiosCupomService },
    { nome: 'Devoluções', componente: DevolucoesListComponent, servico: DevolucoesService },
    { nome: 'Funcionários', componente: FuncionariosListComponent, servico: FuncionariosService },
    { nome: 'Grupos de veículo', componente: GruposVeiculoListComponent, servico: GruposVeiculoService },
    { nome: 'Parceiros', componente: ParceirosListComponent, servico: ParceirosService },
    { nome: 'Planos de cobrança', componente: PlanosCobrancaListComponent, servico: PlanosCobrancaService },
    { nome: 'Taxas e serviços', componente: TaxasServicosListComponent, servico: TaxasServicosService }
  ];

  function montar(componente: Type<unknown>, servico: Type<unknown>, usuarioConfirma: boolean) {
    const excluir = jasmine.createSpy('excluir').and.returnValue(of(undefined));
    // Qualquer outro método do service (listar, listarPessoas...) devolve lista vazia.
    const stub = new Proxy({}, { get: (_, nome) => (nome === 'excluir' ? excluir : () => of([])) });
    const confirmar = jasmine.createSpy('confirmar').and.returnValue(of(usuarioConfirma));

    TestBed.configureTestingModule({
      imports: [componente],
      providers: [
        provideRouter([]),
        { provide: servico, useValue: stub },
        { provide: ClientesService, useValue: stub }, // a lista de condutores também carrega os clientes
        { provide: ConfirmacaoService, useValue: { confirmar } },
        { provide: AuthService, useValue: { getUsuario: () => ({ cargo: 'Empresa' }) } },
        { provide: MatSnackBar, useValue: { open: () => {} } }
      ]
    });
    const fixture = TestBed.createComponent(componente);
    fixture.detectChanges();
    return { instancia: fixture.componentInstance as { excluir(id?: string): void }, excluir, confirmar };
  }

  for (const c of casos) {
    describe(c.nome, () => {
      it('pede confirmação com ação destrutiva e, se o usuário cancela, NÃO exclui', () => {
        const { instancia, excluir, confirmar } = montar(c.componente, c.servico, false);

        instancia.excluir('id-1');

        expect(confirmar).toHaveBeenCalledTimes(1);
        expect(confirmar.calls.mostRecent().args[0]).toEqual(jasmine.objectContaining({ confirmar: 'Excluir', destrutivo: true }));
        expect(excluir).not.toHaveBeenCalled();
      });

      it('se o usuário confirma, exclui o registro certo', () => {
        const { instancia, excluir } = montar(c.componente, c.servico, true);

        instancia.excluir('id-1');

        expect(excluir).toHaveBeenCalledOnceWith('id-1');
      });

      it('sem id não abre diálogo nem exclui', () => {
        const { instancia, excluir, confirmar } = montar(c.componente, c.servico, true);

        instancia.excluir(undefined);

        expect(confirmar).not.toHaveBeenCalled();
        expect(excluir).not.toHaveBeenCalled();
      });
    });
  }
});
