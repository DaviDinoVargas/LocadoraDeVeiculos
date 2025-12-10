import { Routes } from '@angular/router';
import { LoginComponent } from './auth/login.component';
import { RegisterComponent } from './auth/register.component';
import { HomeComponent } from './Components/home/home.component';
import { AuthGuard } from './auth/auth.guard';
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
import { AluguelDevolucaoFormComponent } from './Components/alugueis/aluguel-devolucao-form.component';
import { ConfiguracaoFormComponent } from './Components/configuracoes/configuracao-form.component';
import { DevolucoesListComponent } from './Components/devolucoes/devolucoes-list.component';
import { DevolucaoViewComponent } from './Components/devolucoes/devolucao-view.component';
import { DevolucaoFormComponent } from './Components/devolucoes/devolucao-form.component';
import { CameraMonitorComponent } from './Components/monitoramento/camera-monitor.component';


export const routes: Routes = [
  { path: '', redirectTo: 'home', pathMatch: 'full' },
  { path: 'login', component: LoginComponent },
  { path: 'registrar', component: RegisterComponent },
  { path: 'home', component: HomeComponent, canActivate: [AuthGuard] },

   // Rotas Funcionários
  { path: 'funcionarios', component: FuncionariosListComponent, canActivate: [AuthGuard] },
  { path: 'funcionarios/new', component: FuncionarioFormComponent, canActivate: [AuthGuard] },
  { path: 'funcionarios/:id/edit', component: FuncionarioFormComponent, canActivate: [AuthGuard] },

   // Rotas para Planos de Cobrança
  { path: 'planos-cobranca', component: PlanosCobrancaListComponent },
  { path: 'planos-cobranca/new', component: PlanoCobrancaFormComponent },
  { path: 'planos-cobranca/:id/edit', component: PlanoCobrancaFormComponent },

  // Rotas para Grupos de Veículo
  { path: 'grupoveiculos', component: GruposVeiculoListComponent },
  { path: 'grupoveiculos/new', component: GrupoVeiculoFormComponent },
  { path: 'grupoveiculos/:id/edit', component: GrupoVeiculoFormComponent },

  // Rotas para Veículos
  { path: 'veiculos', component: VeiculosListComponent },
  { path: 'veiculos/new', component: VeiculoFormComponent },
  { path: 'veiculos/:id/edit', component: VeiculoFormComponent },

 { path: 'clientes', component: ClientesListComponent, canActivate: [AuthGuard] },
  { path: 'clientes/pf', component: ClientesListComponent, canActivate: [AuthGuard] },
  { path: 'clientes/pj', component: ClientesListComponent, canActivate: [AuthGuard] },
  { path: 'clientes/new/pf', component: ClientePessoaFisicaFormComponent, canActivate: [AuthGuard] },
  { path: 'clientes/new/pj', component: ClientePessoaJuridicaFormComponent, canActivate: [AuthGuard] },
  { path: 'clientes/:id/edit/pessoa-fisica', component: ClientePessoaFisicaFormComponent, canActivate: [AuthGuard] },
  { path: 'clientes/:id/edit/pessoa-juridica', component: ClientePessoaJuridicaFormComponent, canActivate: [AuthGuard] },

  // Rotas para Aluguéis
{ path: 'alugueis', component: AlugueisListComponent, canActivate: [AuthGuard] },
{ path: 'alugueis/new', component: AluguelFormComponent, canActivate: [AuthGuard] },
{ path: 'alugueis/:id/edit', component: AluguelFormComponent, canActivate: [AuthGuard] },
{ path: 'alugueis/:id/devolver', component: AluguelDevolucaoFormComponent, canActivate: [AuthGuard] },

// Rotas para Configurações
{ path: 'configuracoes/combustivel', component: ConfiguracaoFormComponent, canActivate: [AuthGuard] },

  // Rotas para Condutores
  { path: 'condutores', component: CondutoresListComponent },
  { path: 'condutores/new', component: CondutorFormComponent },
  { path: 'condutores/:id/edit', component: CondutorFormComponent },

// Rotas para Devoluções
{ path: 'devolucoes', component: DevolucoesListComponent, canActivate: [AuthGuard] },
{ path: 'devolucoes/:id', component: DevolucaoViewComponent, canActivate: [AuthGuard] },
{ path: 'alugueis/:id/devolver', component: DevolucaoFormComponent, canActivate: [AuthGuard] },

  // Rotas para Taxas e Serviços
  { path: 'taxas-servicos', component: TaxasServicosListComponent },
  { path: 'taxas-servicos/new', component: TaxaServicoFormComponent },
  { path: 'taxas-servicos/:id/edit', component: TaxaServicoFormComponent },

   {
    path: 'camera-monitor',
    component: CameraMonitorComponent,
    data: { title: 'Monitoramento de Câmeras' }
  },

    { path: '**', redirectTo: 'home' }
];
