import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmNativeSelectImports } from '@spartan-ng/helm/native-select';
import { AuthService } from '../../auth/auth.service';
import { SOMENTE_EMPRESA, cargoPodeAcessar } from '../../core/permissoes';
import { crudLista } from '../../shared/data-table/crud-lista';
import { CelulaDirective, DataTableComponent } from '../../shared/data-table/data-table.component';
import { AcaoLinha, ColunaTabela } from '../../shared/data-table/data-table.types';
import { PageHeaderComponent } from '../../shared/layout/page-header.component';
import { PageComponent } from '../../shared/layout/page.component';
import { GrupoVeiculoDto } from '../grupoveiculos/grupo-veiculo.model';
import { GruposVeiculoService } from '../grupoveiculos/grupos-veiculo.service';
import { VeiculoDto } from './veiculo.model';
import { VeiculosService } from './veiculos.service';

@Component({
  selector: 'app-veiculos-list',
  imports: [
    RouterLink, NgIcon, HlmButtonImports, HlmNativeSelectImports, PageComponent, PageHeaderComponent, DataTableComponent, CelulaDirective
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './veiculos-list.component.html'
})
export class VeiculosListComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly svc = inject(VeiculosService);
  private readonly gruposSvc = inject(GruposVeiculoService);
  private readonly router = inject(Router);

  // Grupo escolhido no filtro ('' = todos). A consulta é feita no servidor (GET /automoveis/grupo/{id}).
  private grupoFiltroId = '';

  private readonly lista = crudLista<VeiculoDto>({
    listar: () => (this.grupoFiltroId ? this.svc.listarPorGrupo(this.grupoFiltroId) : this.svc.listar()),
    excluir: id => this.svc.excluir(id),
    singular: 'veículo',
    carregarAoCriar: false
  });
  readonly veiculos = this.lista.dados;
  readonly carregando = this.lista.carregando;
  readonly erro = this.lista.erro;
  readonly carregar = this.lista.carregar;
  readonly excluir = this.lista.excluir;

  readonly grupos = signal<GrupoVeiculoDto[]>([]);
  // A API só deixa Empresa criar/editar/excluir veículo e só Empresa ler os grupos
  // (o Funcionario tomava 403 ao abrir esta tela por causa do filtro de grupo).
  readonly podeEditar = signal(false);
  private readonly fotosQuebradas = signal<ReadonlySet<string>>(new Set());

  readonly colunas: ColunaTabela<VeiculoDto>[] = [
    { id: 'foto', cabecalho: 'Foto', ordenavel: false },
    { id: 'placa', cabecalho: 'Placa', formatar: v => this.formatarPlaca(v.placa) },
    { id: 'marca', cabecalho: 'Marca' },
    { id: 'modelo', cabecalho: 'Modelo' },
    { id: 'cor', cabecalho: 'Cor', prioridade: 'baixa' },
    { id: 'ano', cabecalho: 'Ano', alinhar: 'inicio', formatar: v => String(v.ano ?? '—') },
    { id: 'tipoCombustivel', cabecalho: 'Combustível', filtravel: true },
    { id: 'capacidadeTanque', cabecalho: 'Tanque (L)', tipo: 'numero', prioridade: 'baixa' },
    { id: 'grupo', cabecalho: 'Grupo', valor: v => this.obterNomeGrupo(v) }
  ];

  readonly acoes = computed<AcaoLinha<VeiculoDto>[]>(() =>
    this.podeEditar()
      ? [
          { rotulo: 'Editar', icone: 'lucidePencil', executar: v => this.editar(v.id) },
          { rotulo: 'Excluir', icone: 'lucideTrash2', destrutiva: true, executar: v => this.excluir(v.id) }
        ]
      : []
  );

  readonly tituloLinha = (v: VeiculoDto) => `${v.marca} ${v.modelo}`.trim();

  ngOnInit(): void {
    this.podeEditar.set(cargoPodeAcessar(this.auth.getUsuario()?.cargo, SOMENTE_EMPRESA));
    if (this.podeEditar()) {
      this.carregarGrupos();
    }
    this.carregar();
  }

  carregarGrupos(): void {
    this.gruposSvc.listar().subscribe({ next: grupos => this.grupos.set(grupos) });
  }

  // Antes o <select> alterava veiculosFiltrados mas a tabela renderizava `veiculos`: o filtro
  // não fazia nada na tela.
  filtrarPorGrupo(grupoId: string): void {
    this.grupoFiltroId = grupoId;
    this.carregar();
  }

  // O nome vem na própria lista (o Funcionario não pode ler /grupos-automovel para resolvê-lo).
  obterNomeGrupo(veiculo: VeiculoDto): string {
    return veiculo.grupoAutomovelNome || this.grupos().find(g => g.id === veiculo.grupoAutomovelId)?.nome || '—';
  }

  formatarPlaca(placa: string): string {
    if (!placa) return '';
    // Formata placa no padrão: AAA-0A00 ou AAA-0000
    return placa.replace(/([A-Za-z]{3})([0-9A-Za-z]{4})/, '$1-$2').toUpperCase();
  }

  fotoVisivel(veiculo: VeiculoDto): boolean {
    return !!veiculo.foto && !this.fotosQuebradas().has(veiculo.id ?? veiculo.placa);
  }

  fotoQuebrou(veiculo: VeiculoDto): void {
    this.fotosQuebradas.update(s => new Set(s).add(veiculo.id ?? veiculo.placa));
  }

  novo(): void {
    void this.router.navigate(['/veiculos/new']);
  }

  editar(id?: string): void {
    if (id) void this.router.navigate([`/veiculos/${id}/edit`]);
  }
}
