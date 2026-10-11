import { CanDeactivateFn, Route, Routes } from '@angular/router';
import { AuthGuard } from './auth/auth.guard';
import { formularioSemPerdaGuard } from './core/form-guard';
import { EMPRESA_E_FUNCIONARIO, SOMENTE_EMPRESA } from './core/permissoes';
import { roleGuard } from './core/role.guard';

// Toda tela é carregada sob demanda (loadComponent): o pacote inicial só leva o login e a casca.
// `protegida` aplica o AuthGuard + roleGuard e declara os papéis (core/permissoes.ts é a fonte única;
// o spec de rotas falha se uma rota protegida ficar sem `data.roles`).
const protegida = (
  path: string,
  loadComponent: Route['loadComponent'],
  roles: readonly string[],
  extra: { title?: string; data?: Record<string, unknown>; canDeactivate?: Route['canDeactivate'] } = {}
): Route => ({
  path,
  loadComponent,
  canActivate: [AuthGuard, roleGuard],
  data: { roles, ...extra.data },
  ...(extra.title ? { title: extra.title } : {}),
  ...(extra.canDeactivate ? { canDeactivate: extra.canDeactivate } : {})
});

/** Formulários de cadastro/edição: avisam antes de descartar alterações não salvas. */
const formulario = (path: string, loadComponent: Route['loadComponent'], roles: readonly string[], title: string): Route =>
  protegida(path, loadComponent, roles, { title, canDeactivate: [formularioSemPerdaGuard as unknown as CanDeactivateFn<unknown>] });

export const routes: Routes = [
  { path: '', redirectTo: 'home', pathMatch: 'full' },
  { path: 'login', loadComponent: () => import('./auth/login.component').then(m => m.LoginComponent), title: 'Entrar' },
  { path: 'registrar', loadComponent: () => import('./auth/register.component').then(m => m.RegisterComponent), title: 'Criar conta' },
  protegida('home', () => import('./Components/home/home.component').then(m => m.HomeComponent), EMPRESA_E_FUNCIONARIO, { title: 'Início' }),

  // Funcionários
  protegida('funcionarios', () => import('./Components/funcionarios/funcionarios-list.component').then(m => m.FuncionariosListComponent), SOMENTE_EMPRESA, { title: 'Funcionários' }),
  formulario('funcionarios/new', () => import('./Components/funcionarios/funcionario-form.component').then(m => m.FuncionarioFormComponent), SOMENTE_EMPRESA, 'Novo funcionário'),
  formulario('funcionarios/:id/edit', () => import('./Components/funcionarios/funcionario-form.component').then(m => m.FuncionarioFormComponent), SOMENTE_EMPRESA, 'Editar funcionário'),

  // Planos de cobrança
  protegida('planos-cobranca', () => import('./Components/planocobranca/planos-cobranca-list.component').then(m => m.PlanosCobrancaListComponent), SOMENTE_EMPRESA, { title: 'Planos de cobrança' }),
  formulario('planos-cobranca/new', () => import('./Components/planocobranca/plano-cobranca-form.component').then(m => m.PlanoCobrancaFormComponent), SOMENTE_EMPRESA, 'Novo plano de cobrança'),
  formulario('planos-cobranca/:id/edit', () => import('./Components/planocobranca/plano-cobranca-form.component').then(m => m.PlanoCobrancaFormComponent), SOMENTE_EMPRESA, 'Editar plano de cobrança'),

  // Grupos de veículo
  protegida('grupoveiculos', () => import('./Components/grupoveiculos/grupos-veiculo-list.component').then(m => m.GruposVeiculoListComponent), SOMENTE_EMPRESA, { title: 'Grupos de veículo' }),
  formulario('grupoveiculos/new', () => import('./Components/grupoveiculos/grupo-veiculo-form.component').then(m => m.GrupoVeiculoFormComponent), SOMENTE_EMPRESA, 'Novo grupo de veículo'),
  formulario('grupoveiculos/:id/edit', () => import('./Components/grupoveiculos/grupo-veiculo-form.component').then(m => m.GrupoVeiculoFormComponent), SOMENTE_EMPRESA, 'Editar grupo de veículo'),

  // Veículos
  protegida('veiculos', () => import('./Components/veiculos/veiculos-list.component').then(m => m.VeiculosListComponent), EMPRESA_E_FUNCIONARIO, { title: 'Veículos' }),
  formulario('veiculos/new', () => import('./Components/veiculos/veiculo-form.component').then(m => m.VeiculoFormComponent), SOMENTE_EMPRESA, 'Novo veículo'),
  formulario('veiculos/:id/edit', () => import('./Components/veiculos/veiculo-form.component').then(m => m.VeiculoFormComponent), SOMENTE_EMPRESA, 'Editar veículo'),

  // Clientes (lista geral, pessoas físicas e jurídicas)
  protegida('clientes', () => import('./Components/clientes/clientes-list.component').then(m => m.ClientesListComponent), EMPRESA_E_FUNCIONARIO, { title: 'Clientes' }),
  protegida('clientes/pf', () => import('./Components/clientes/clientes-list.component').then(m => m.ClientesListComponent), EMPRESA_E_FUNCIONARIO, { title: 'Pessoas físicas' }),
  protegida('clientes/pj', () => import('./Components/clientes/clientes-list.component').then(m => m.ClientesListComponent), EMPRESA_E_FUNCIONARIO, { title: 'Pessoas jurídicas' }),
  formulario('clientes/new/pf', () => import('./Components/clientes/ClientePessoaFisicaFormComponent').then(m => m.ClientePessoaFisicaFormComponent), EMPRESA_E_FUNCIONARIO, 'Nova pessoa física'),
  formulario('clientes/new/pj', () => import('./Components/clientes/ClientePessoaJuridicaFormComponent').then(m => m.ClientePessoaJuridicaFormComponent), EMPRESA_E_FUNCIONARIO, 'Nova pessoa jurídica'),
  formulario('clientes/:id/edit/pessoa-fisica', () => import('./Components/clientes/ClientePessoaFisicaFormComponent').then(m => m.ClientePessoaFisicaFormComponent), EMPRESA_E_FUNCIONARIO, 'Editar pessoa física'),
  formulario('clientes/:id/edit/pessoa-juridica', () => import('./Components/clientes/ClientePessoaJuridicaFormComponent').then(m => m.ClientePessoaJuridicaFormComponent), EMPRESA_E_FUNCIONARIO, 'Editar pessoa jurídica'),

  // Aluguéis
  protegida('alugueis', () => import('./Components/alugueis/alugueis-list.component').then(m => m.AlugueisListComponent), EMPRESA_E_FUNCIONARIO, { title: 'Aluguéis' }),
  formulario('alugueis/new', () => import('./Components/alugueis/aluguel-form.component').then(m => m.AluguelFormComponent), EMPRESA_E_FUNCIONARIO, 'Novo aluguel'),
  formulario('alugueis/:id/edit', () => import('./Components/alugueis/aluguel-form.component').then(m => m.AluguelFormComponent), EMPRESA_E_FUNCIONARIO, 'Editar aluguel'),

  // Configurações
  formulario('configuracoes/combustivel', () => import('./Components/configuracoes/configuracao-form.component').then(m => m.ConfiguracaoFormComponent), SOMENTE_EMPRESA, 'Configurações'),

  // Condutores
  protegida('condutores', () => import('./Components/condutor/condutores-list.component').then(m => m.CondutoresListComponent), EMPRESA_E_FUNCIONARIO, { title: 'Condutores' }),
  formulario('condutores/new', () => import('./Components/condutor/condutor-form.component').then(m => m.CondutorFormComponent), EMPRESA_E_FUNCIONARIO, 'Novo condutor'),
  formulario('condutores/:id/edit', () => import('./Components/condutor/condutor-form.component').then(m => m.CondutorFormComponent), EMPRESA_E_FUNCIONARIO, 'Editar condutor'),

  // Devoluções
  protegida('devolucoes', () => import('./Components/devolucoes/devolucoes-list.component').then(m => m.DevolucoesListComponent), EMPRESA_E_FUNCIONARIO, { title: 'Devoluções' }),
  protegida('devolucoes/:id', () => import('./Components/devolucoes/devolucao-view.component').then(m => m.DevolucaoViewComponent), EMPRESA_E_FUNCIONARIO, { title: 'Devolução' }),
  formulario('alugueis/:id/devolver', () => import('./Components/devolucoes/devolucao-form.component').then(m => m.DevolucaoFormComponent), EMPRESA_E_FUNCIONARIO, 'Registrar devolução'),

  // Taxas e serviços
  protegida('taxas-servicos', () => import('./Components/taxa-servico/taxas-servicos-list.component').then(m => m.TaxasServicosListComponent), EMPRESA_E_FUNCIONARIO, { title: 'Taxas e serviços' }),
  formulario('taxas-servicos/new', () => import('./Components/taxa-servico/taxa-servico-form.component').then(m => m.TaxaServicoFormComponent), EMPRESA_E_FUNCIONARIO, 'Nova taxa ou serviço'),
  formulario('taxas-servicos/:id/edit', () => import('./Components/taxa-servico/taxa-servico-form.component').then(m => m.TaxaServicoFormComponent), EMPRESA_E_FUNCIONARIO, 'Editar taxa ou serviço'),

  protegida('camera-monitor', () => import('./Components/monitoramento/camera-monitor.component').then(m => m.CameraMonitorComponent), EMPRESA_E_FUNCIONARIO, {
    title: 'Monitoramento de câmeras',
    data: { title: 'Monitoramento de Câmeras' }
  }),
  protegida('verificacao-facial', () => import('./Components/verificacao-facial/verificacao-facial.component').then(m => m.VerificacaoFacialComponent), EMPRESA_E_FUNCIONARIO, {
    title: 'Verificação facial',
    data: { title: 'Verificação Facial' }
  }),

  // Parceiros
  protegida('parceiros', () => import('./Components/parceiros/parceiros-list.component').then(m => m.ParceirosListComponent), SOMENTE_EMPRESA, { title: 'Parceiros' }),
  formulario('parceiros/new', () => import('./Components/parceiros/parceiro-form.component').then(m => m.ParceiroFormComponent), SOMENTE_EMPRESA, 'Novo parceiro'),
  formulario('parceiros/:id/edit', () => import('./Components/parceiros/parceiro-form.component').then(m => m.ParceiroFormComponent), SOMENTE_EMPRESA, 'Editar parceiro'),

  // Cupons
  protegida('cupons', () => import('./Components/cupons/cupons-list.component').then(m => m.CuponsListComponent), SOMENTE_EMPRESA, { title: 'Cupons' }),
  formulario('cupons/new', () => import('./Components/cupons/cupom-form.component').then(m => m.CupomFormComponent), SOMENTE_EMPRESA, 'Novo cupom'),
  formulario('cupons/:id/edit', () => import('./Components/cupons/cupom-form.component').then(m => m.CupomFormComponent), SOMENTE_EMPRESA, 'Editar cupom'),

  // Desafios de cupom
  protegida('desafios-cupom', () => import('./Components/desafios-cupom/desafios-cupom-list.component').then(m => m.DesafiosCupomListComponent), SOMENTE_EMPRESA, { title: 'Desafios de cupom' }),
  formulario('desafios-cupom/new', () => import('./Components/desafios-cupom/desafio-cupom-form.component').then(m => m.DesafioCupomFormComponent), SOMENTE_EMPRESA, 'Novo desafio de cupom'),
  formulario('desafios-cupom/:id/edit', () => import('./Components/desafios-cupom/desafio-cupom-form.component').then(m => m.DesafioCupomFormComponent), SOMENTE_EMPRESA, 'Editar desafio de cupom'),

  { path: '**', redirectTo: 'home' }
];
