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

    { path: '**', redirectTo: 'home' }
];
