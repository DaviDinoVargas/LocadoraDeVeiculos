import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { VeiculosService } from './veiculos.service';
import { GruposVeiculoService } from '../grupoveiculos/grupos-veiculo.service';
import { VeiculoDto } from './veiculo.model';
import { GrupoVeiculoDto } from '../grupoveiculos/grupo-veiculo.model';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { AuthService } from '../../auth/auth.service';
import { SOMENTE_EMPRESA, cargoPodeAcessar } from '../../core/permissoes';

@Component({
  selector: 'app-veiculos-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './veiculos-list.component.html',
  styleUrls: ['../scss/global.scss']
})
export class VeiculosListComponent implements OnInit {
  veiculos: VeiculoDto[] = [];
  veiculosFiltrados: VeiculoDto[] = [];
  grupos: GrupoVeiculoDto[] = [];
  loading = false;
  // A API só deixa Empresa criar/editar/excluir veículo e só Empresa ler os grupos
  // (o Funcionario tomava 403 ao abrir esta tela por causa do filtro de grupo).
  podeEditar = false;

  constructor(
    private auth: AuthService,
    private svc: VeiculosService,
    private gruposSvc: GruposVeiculoService,
    private router: Router,
    private snack: MatSnackBar
  ) {}

  ngOnInit() {
    this.podeEditar = cargoPodeAcessar(this.auth.getUsuario()?.cargo, SOMENTE_EMPRESA);
    if (this.podeEditar) {
      this.carregarGrupos();
    }
    this.carregar();
  }

  carregarGrupos() {
    this.gruposSvc.listar().subscribe({
      next: grupos => this.grupos = grupos,
      error: () => this.snack.open('Erro ao carregar grupos', 'Fechar', { duration: 4000 })
    });
  }

  // Grupo escolhido no filtro ('' = todos). A consulta é feita no servidor (GET /automoveis/grupo/{id}).
  private grupoFiltroId = '';

  carregar() {
    this.loading = true;
    const origem$ = this.grupoFiltroId ? this.svc.listarPorGrupo(this.grupoFiltroId) : this.svc.listar();
    origem$.subscribe({
      next: v => {
        this.veiculos = v;
        this.veiculosFiltrados = [...v];
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        console.error('Erro ao listar veículos:', err);
        this.snack.open('Erro ao listar veículos', 'Fechar', { duration: 4000 });
      }
    });
  }

  // Antes o <select> alterava veiculosFiltrados mas a tabela renderizava `veiculos`: o filtro
  // não fazia nada na tela.
  filtrarPorGrupo(event: any) {
    this.grupoFiltroId = event.target.value;
    this.carregar();
  }

  // O nome vem na própria lista (o Funcionario não pode ler /grupos-automovel para resolvê-lo).
  obterNomeGrupo(veiculo: VeiculoDto): string {
    return veiculo.grupoAutomovelNome || this.grupos.find(g => g.id === veiculo.grupoAutomovelId)?.nome || '—';
  }

  formatarPlaca(placa: string): string {
    if (!placa) return '';
    // Formata placa no padrão: AAA-0A00 ou AAA-0000
    return placa.replace(/([A-Za-z]{3})([0-9A-Za-z]{4})/, '$1-$2').toUpperCase();
  }

  novo() {
    this.router.navigate(['/veiculos/new']);
  }

  editar(id?: string) {
    if (id) this.router.navigate([`/veiculos/${id}/edit`]);
  }

  excluir(id?: string) {
    if (!id || !confirm('Deseja realmente excluir este veículo?')) return;

    this.svc.excluir(id).subscribe({
      next: () => {
        this.snack.open('Veículo excluído', 'Fechar', { duration: 3000 });
        this.carregar();
      },
      error: (err) => {
        console.error('Erro ao excluir veículo:', err);
        this.snack.open('Falha ao excluir veículo', 'Fechar', { duration: 4000 });
      }
    });
  }
}
