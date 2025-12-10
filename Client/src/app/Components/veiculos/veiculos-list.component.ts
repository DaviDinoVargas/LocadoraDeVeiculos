import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { VeiculosService } from './veiculos.service';
import { GruposVeiculoService } from '../grupoveiculos/grupos-veiculo.service';
import { VeiculoDto } from './veiculo.model';
import { GrupoVeiculoDto } from '../grupoveiculos/grupo-veiculo.model';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';

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

  constructor(
    private svc: VeiculosService,
    private gruposSvc: GruposVeiculoService,
    private router: Router,
    private snack: MatSnackBar
  ) {}

  ngOnInit() {
    this.carregarGrupos();
    this.carregar();
  }

  carregarGrupos() {
    this.gruposSvc.listar().subscribe({
      next: grupos => this.grupos = grupos,
      error: () => this.snack.open('Erro ao carregar grupos', 'Fechar', { duration: 4000 })
    });
  }

  carregar() {
    this.loading = true;
    this.svc.listar().subscribe({
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

  filtrarPorGrupo(event: any) {
    const grupoId = event.target.value;
    if (!grupoId) {
      this.veiculosFiltrados = [...this.veiculos];
    } else {
      this.veiculosFiltrados = this.veiculos.filter(v => v.grupoAutomovelId === grupoId);
    }
  }

  obterNomeGrupo(grupoId: string): string {
    const grupo = this.grupos.find(g => g.id === grupoId);
    return grupo ? grupo.nome : grupoId;
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
