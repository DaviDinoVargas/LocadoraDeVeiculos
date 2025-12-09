import { Routes } from '@angular/router';
import { LoginComponent } from './auth/login.component';
import { RegisterComponent } from './auth/register.component';
import { HomeComponent } from './Components/home/home.component';
import { AuthGuard } from './auth/auth.guard';
import { FuncionariosListComponent } from './Components/funcionarios/funcionarios-list.component';
import { FuncionarioFormComponent } from './Components/funcionarios/funcionario-form.component';

export const routes: Routes = [
  { path: '', redirectTo: 'home', pathMatch: 'full' },
  { path: 'login', component: LoginComponent },
  { path: 'registrar', component: RegisterComponent },
  { path: 'home', component: HomeComponent, canActivate: [AuthGuard] },

   // Rotas Funcionários
  { path: 'funcionarios', component: FuncionariosListComponent, canActivate: [AuthGuard] },
  { path: 'funcionarios/new', component: FuncionarioFormComponent, canActivate: [AuthGuard] },
  { path: 'funcionarios/:id/edit', component: FuncionarioFormComponent, canActivate: [AuthGuard] },


    { path: '**', redirectTo: 'home' }
];
