import { Routes } from '@angular/router';
import { LoginComponent } from './auth/login.component';
import { RegisterComponent } from './auth/register.component';
import { HomeComponent } from './Components/home/home.component';
import { AuthGuard } from './auth/auth.guard';
import { roleGuard } from './core/role.guard';
import { EMPRESA_E_FUNCIONARIO, SOMENTE_EMPRESA } from './core/permissoes';
import { FuncionariosListComponent } from './Components/funcionarios/funcionarios-list.component';
import { FuncionarioFormComponent } from './Components/funcionarios/funcionario-form.component';
import { PlanosCobrancaListComponent } from './Components/planocobranca/planos-cobranca-list.component';
import { PlanoCobrancaFormComponent } from './Components/planocobranca/plano-cobranca-form.component';
import { GruposVeiculoListComponent } from './Components/grupoveiculos/grupos-veiculo-list.component';
import { GrupoVeiculoFormComponent } from './Components/grupoveiculos/grupo-veiculo-form.component';
import { VeiculosListComponent } from './Components/veiculos/veiculos-list.component';
import { VeiculoFormComponent } from './Components/veiculos/veiculo-form.component';
import { ClientesListComponent } from './Components/clientes/clientes-list.component';
import { CondutoresListComponent } from './Components/condutor/condutores-list.component';
import { CondutorFormComponent } from './Components/condutor/condutor-form.component';
import { TaxasServicosListComponent } from './Components/taxa-servico/taxas-servicos-list.component';
import { TaxaServicoFormComponent } from './Components/taxa-servico/taxa-servico-form.component';
import { ClientePessoaFisicaFormComponent } from './Components/clientes/ClientePessoaFisicaFormComponent';
import { ClientePessoaJuridicaFormComponent } from './Components/clientes/ClientePessoaJuridicaFormComponent';
import { AlugueisListComponent } from './Components/alugueis/alugueis-list.component';
import { AluguelFormComponent } from './Components/alugueis/aluguel-form.component';
import { ConfiguracaoFormComponent } from './Components/configuracoes/configuracao-form.component';
import { DevolucoesListComponent } from './Components/devolucoes/devolucoes-list.component';
import { DevolucaoViewComponent } from './Components/devolucoes/devolucao-view.component';
import { DevolucaoFormComponent } from './Components/devolucoes/devolucao-form.component';
import { CameraMonitorComponent } from './Components/monitoramento/camera-monitor.component';
import { VerificacaoFacialComponent } from './Components/verificacao-facial/verificacao-facial.component';
import { ParceirosListComponent } from './Components/parceiros/parceiros-list.component';
import { ParceiroFormComponent } from './Components/parceiros/parceiro-form.component';
import { CuponsListComponent } from './Components/cupons/cupons-list.component';
import { CupomFormComponent } from './Components/cupons/cupom-form.component';
import { DesafiosCupomListComponent } from './Components/desafios-cupom/desafios-cupom-list.component';
import { DesafioCupomFormComponent } from './Components/desafios-cupom/desafio-cupom-form.component';


export const routes: Routes = [
  { path: '', redirectTo: 'home', pathMatch: 'full' },
  { path: 'login', component: LoginComponent },
  { path: 'registrar', component: RegisterComponent },
  { path: 'home', component: HomeComponent, canActivate: [AuthGuard, roleGuard], data: { roles: EMPRESA_E_FUNCIONARIO } },

   // Rotas Funcionários
  { path: 'funcionarios', component: FuncionariosListComponent, canActivate: [AuthGuard, roleGuard], data: { roles: SOMENTE_EMPRESA } },
  { path: 'funcionarios/new', component: FuncionarioFormComponent, canActivate: [AuthGuard, roleGuard], data: { roles: SOMENTE_EMPRESA } },
  { path: 'funcionarios/:id/edit', component: FuncionarioFormComponent, canActivate: [AuthGuard, roleGuard], data: { roles: SOMENTE_EMPRESA } },

   // Rotas para Planos de Cobrança
  { path: 'planos-cobranca', component: PlanosCobrancaListComponent, canActivate: [AuthGuard, roleGuard], data: { roles: SOMENTE_EMPRESA } },
  { path: 'planos-cobranca/new', component: PlanoCobrancaFormComponent, canActivate: [AuthGuard, roleGuard], data: { roles: SOMENTE_EMPRESA } },
  { path: 'planos-cobranca/:id/edit', component: PlanoCobrancaFormComponent, canActivate: [AuthGuard, roleGuard], data: { roles: SOMENTE_EMPRESA } },

  // Rotas para Grupos de Veículo
  { path: 'grupoveiculos', component: GruposVeiculoListComponent, canActivate: [AuthGuard, roleGuard], data: { roles: SOMENTE_EMPRESA } },
  { path: 'grupoveiculos/new', component: GrupoVeiculoFormComponent, canActivate: [AuthGuard, roleGuard], data: { roles: SOMENTE_EMPRESA } },
  { path: 'grupoveiculos/:id/edit', component: GrupoVeiculoFormComponent, canActivate: [AuthGuard, roleGuard], data: { roles: SOMENTE_EMPRESA } },

  // Rotas para Veículos
  { path: 'veiculos', component: VeiculosListComponent, canActivate: [AuthGuard, roleGuard], data: { roles: EMPRESA_E_FUNCIONARIO } },
  { path: 'veiculos/new', component: VeiculoFormComponent, canActivate: [AuthGuard, roleGuard], data: { roles: SOMENTE_EMPRESA } },
  { path: 'veiculos/:id/edit', component: VeiculoFormComponent, canActivate: [AuthGuard, roleGuard], data: { roles: SOMENTE_EMPRESA } },

 { path: 'clientes', component: ClientesListComponent, canActivate: [AuthGuard, roleGuard], data: { roles: EMPRESA_E_FUNCIONARIO } },
  { path: 'clientes/pf', component: ClientesListComponent, canActivate: [AuthGuard, roleGuard], data: { roles: EMPRESA_E_FUNCIONARIO } },
  { path: 'clientes/pj', component: ClientesListComponent, canActivate: [AuthGuard, roleGuard], data: { roles: EMPRESA_E_FUNCIONARIO } },
  { path: 'clientes/new/pf', component: ClientePessoaFisicaFormComponent, canActivate: [AuthGuard, roleGuard], data: { roles: EMPRESA_E_FUNCIONARIO } },
  { path: 'clientes/new/pj', component: ClientePessoaJuridicaFormComponent, canActivate: [AuthGuard, roleGuard], data: { roles: EMPRESA_E_FUNCIONARIO } },
  { path: 'clientes/:id/edit/pessoa-fisica', component: ClientePessoaFisicaFormComponent, canActivate: [AuthGuard, roleGuard], data: { roles: EMPRESA_E_FUNCIONARIO } },
  { path: 'clientes/:id/edit/pessoa-juridica', component: ClientePessoaJuridicaFormComponent, canActivate: [AuthGuard, roleGuard], data: { roles: EMPRESA_E_FUNCIONARIO } },

  // Rotas para Aluguéis
{ path: 'alugueis', component: AlugueisListComponent, canActivate: [AuthGuard, roleGuard], data: { roles: EMPRESA_E_FUNCIONARIO } },
{ path: 'alugueis/new', component: AluguelFormComponent, canActivate: [AuthGuard, roleGuard], data: { roles: EMPRESA_E_FUNCIONARIO } },
{ path: 'alugueis/:id/edit', component: AluguelFormComponent, canActivate: [AuthGuard, roleGuard], data: { roles: EMPRESA_E_FUNCIONARIO } },

// Rotas para Configurações
{ path: 'configuracoes/combustivel', component: ConfiguracaoFormComponent, canActivate: [AuthGuard, roleGuard], data: { roles: SOMENTE_EMPRESA } },

  // Rotas para Condutores
  { path: 'condutores', component: CondutoresListComponent, canActivate: [AuthGuard, roleGuard], data: { roles: EMPRESA_E_FUNCIONARIO } },
  { path: 'condutores/new', component: CondutorFormComponent, canActivate: [AuthGuard, roleGuard], data: { roles: EMPRESA_E_FUNCIONARIO } },
  { path: 'condutores/:id/edit', component: CondutorFormComponent, canActivate: [AuthGuard, roleGuard], data: { roles: EMPRESA_E_FUNCIONARIO } },

// Rotas para Devoluções
{ path: 'devolucoes', component: DevolucoesListComponent, canActivate: [AuthGuard, roleGuard], data: { roles: EMPRESA_E_FUNCIONARIO } },
{ path: 'devolucoes/:id', component: DevolucaoViewComponent, canActivate: [AuthGuard, roleGuard], data: { roles: EMPRESA_E_FUNCIONARIO } },
{ path: 'alugueis/:id/devolver', component: DevolucaoFormComponent, canActivate: [AuthGuard, roleGuard], data: { roles: EMPRESA_E_FUNCIONARIO } },

  // Rotas para Taxas e Serviços
  { path: 'taxas-servicos', component: TaxasServicosListComponent, canActivate: [AuthGuard, roleGuard], data: { roles: EMPRESA_E_FUNCIONARIO } },
  { path: 'taxas-servicos/new', component: TaxaServicoFormComponent, canActivate: [AuthGuard, roleGuard], data: { roles: EMPRESA_E_FUNCIONARIO } },
  { path: 'taxas-servicos/:id/edit', component: TaxaServicoFormComponent, canActivate: [AuthGuard, roleGuard], data: { roles: EMPRESA_E_FUNCIONARIO } },

   {
    path: 'camera-monitor',
    component: CameraMonitorComponent,
    canActivate: [AuthGuard, roleGuard],
    data: { roles: EMPRESA_E_FUNCIONARIO, title: 'Monitoramento de Câmeras' }
  },

  {
    path: 'verificacao-facial',
    component: VerificacaoFacialComponent,
    canActivate: [AuthGuard, roleGuard],
    data: { roles: EMPRESA_E_FUNCIONARIO, title: 'Verificação Facial' }
  },

  // Rotas para Parceiros
  { path: 'parceiros', component: ParceirosListComponent, canActivate: [AuthGuard, roleGuard], data: { roles: SOMENTE_EMPRESA } },
  { path: 'parceiros/new', component: ParceiroFormComponent, canActivate: [AuthGuard, roleGuard], data: { roles: SOMENTE_EMPRESA } },
  { path: 'parceiros/:id/edit', component: ParceiroFormComponent, canActivate: [AuthGuard, roleGuard], data: { roles: SOMENTE_EMPRESA } },

  // Rotas para Cupons
  { path: 'cupons', component: CuponsListComponent, canActivate: [AuthGuard, roleGuard], data: { roles: SOMENTE_EMPRESA } },
  { path: 'cupons/new', component: CupomFormComponent, canActivate: [AuthGuard, roleGuard], data: { roles: SOMENTE_EMPRESA } },
  { path: 'cupons/:id/edit', component: CupomFormComponent, canActivate: [AuthGuard, roleGuard], data: { roles: SOMENTE_EMPRESA } },

  // Rotas para Desafios de Cupom
  { path: 'desafios-cupom', component: DesafiosCupomListComponent, canActivate: [AuthGuard, roleGuard], data: { roles: SOMENTE_EMPRESA } },
  { path: 'desafios-cupom/new', component: DesafioCupomFormComponent, canActivate: [AuthGuard, roleGuard], data: { roles: SOMENTE_EMPRESA } },
  { path: 'desafios-cupom/:id/edit', component: DesafioCupomFormComponent, canActivate: [AuthGuard, roleGuard], data: { roles: SOMENTE_EMPRESA } },

    { path: '**', redirectTo: 'home' }
];
