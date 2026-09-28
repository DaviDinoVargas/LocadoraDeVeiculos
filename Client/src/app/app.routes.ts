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
  { path: 'home', component: HomeComponent, canActivate: [AuthGuard] },

   // Rotas Funcionários
  { path: 'funcionarios', component: FuncionariosListComponent, canActivate: [AuthGuard] },
  { path: 'funcionarios/new', component: FuncionarioFormComponent, canActivate: [AuthGuard] },
  { path: 'funcionarios/:id/edit', component: FuncionarioFormComponent, canActivate: [AuthGuard] },

   // Rotas para Planos de Cobrança
  { path: 'planos-cobranca', component: PlanosCobrancaListComponent, canActivate: [AuthGuard] },
  { path: 'planos-cobranca/new', component: PlanoCobrancaFormComponent, canActivate: [AuthGuard] },
  { path: 'planos-cobranca/:id/edit', component: PlanoCobrancaFormComponent, canActivate: [AuthGuard] },

  // Rotas para Grupos de Veículo
  { path: 'grupoveiculos', component: GruposVeiculoListComponent, canActivate: [AuthGuard] },
  { path: 'grupoveiculos/new', component: GrupoVeiculoFormComponent, canActivate: [AuthGuard] },
  { path: 'grupoveiculos/:id/edit', component: GrupoVeiculoFormComponent, canActivate: [AuthGuard] },

  // Rotas para Veículos
  { path: 'veiculos', component: VeiculosListComponent, canActivate: [AuthGuard] },
  { path: 'veiculos/new', component: VeiculoFormComponent, canActivate: [AuthGuard] },
  { path: 'veiculos/:id/edit', component: VeiculoFormComponent, canActivate: [AuthGuard] },

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

// Rotas para Configurações
{ path: 'configuracoes/combustivel', component: ConfiguracaoFormComponent, canActivate: [AuthGuard] },

  // Rotas para Condutores
  { path: 'condutores', component: CondutoresListComponent, canActivate: [AuthGuard] },
  { path: 'condutores/new', component: CondutorFormComponent, canActivate: [AuthGuard] },
  { path: 'condutores/:id/edit', component: CondutorFormComponent, canActivate: [AuthGuard] },

// Rotas para Devoluções
{ path: 'devolucoes', component: DevolucoesListComponent, canActivate: [AuthGuard] },
{ path: 'devolucoes/:id', component: DevolucaoViewComponent, canActivate: [AuthGuard] },
{ path: 'alugueis/:id/devolver', component: DevolucaoFormComponent, canActivate: [AuthGuard] },

  // Rotas para Taxas e Serviços
  { path: 'taxas-servicos', component: TaxasServicosListComponent, canActivate: [AuthGuard] },
  { path: 'taxas-servicos/new', component: TaxaServicoFormComponent, canActivate: [AuthGuard] },
  { path: 'taxas-servicos/:id/edit', component: TaxaServicoFormComponent, canActivate: [AuthGuard] },

   {
    path: 'camera-monitor',
    component: CameraMonitorComponent,
    canActivate: [AuthGuard],
    data: { title: 'Monitoramento de Câmeras' }
  },

  {
    path: 'verificacao-facial',
    component: VerificacaoFacialComponent,
    canActivate: [AuthGuard],
    data: { title: 'Verificação Facial' }
  },

  // Rotas para Parceiros
  { path: 'parceiros', component: ParceirosListComponent, canActivate: [AuthGuard] },
  { path: 'parceiros/new', component: ParceiroFormComponent, canActivate: [AuthGuard] },
  { path: 'parceiros/:id/edit', component: ParceiroFormComponent, canActivate: [AuthGuard] },

  // Rotas para Cupons
  { path: 'cupons', component: CuponsListComponent, canActivate: [AuthGuard] },
  { path: 'cupons/new', component: CupomFormComponent, canActivate: [AuthGuard] },
  { path: 'cupons/:id/edit', component: CupomFormComponent, canActivate: [AuthGuard] },

  // Rotas para Desafios de Cupom
  { path: 'desafios-cupom', component: DesafiosCupomListComponent, canActivate: [AuthGuard] },
  { path: 'desafios-cupom/new', component: DesafioCupomFormComponent, canActivate: [AuthGuard] },
  { path: 'desafios-cupom/:id/edit', component: DesafioCupomFormComponent, canActivate: [AuthGuard] },

    { path: '**', redirectTo: 'home' }
];
