// home.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../auth/auth.service';
import { EMPRESA_E_FUNCIONARIO, SOMENTE_EMPRESA, cargoPodeAcessar } from '../../core/permissoes';

interface AtalhoDashboard {
  titulo: string;
  descricao: string;
  icone: string;
  rota: string;
  roles: readonly string[];
}

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss']
})
export class HomeComponent implements OnInit {
  nomeUsuario = '';
  atalhos: AtalhoDashboard[] = [];

  private readonly todosAtalhos: AtalhoDashboard[] = [
    { titulo: 'Veículos', descricao: 'Cadastro de veículos, grupos e planos de cobrança.', icone: 'bi-car-front', rota: '/veiculos', roles: EMPRESA_E_FUNCIONARIO },
    { titulo: 'Clientes', descricao: 'Pessoas físicas e jurídicas cadastradas.', icone: 'bi-people', rota: '/clientes', roles: EMPRESA_E_FUNCIONARIO },
    { titulo: 'Condutores', descricao: 'Condutores habilitados para locação.', icone: 'bi-person-badge', rota: '/condutores', roles: EMPRESA_E_FUNCIONARIO },
    { titulo: 'Aluguéis', descricao: 'Locações em aberto, reservas e histórico.', icone: 'bi-receipt', rota: '/alugueis', roles: EMPRESA_E_FUNCIONARIO },
    { titulo: 'Devoluções', descricao: 'Registro de devolução e recibo em PDF.', icone: 'bi-arrow-return-left', rota: '/devolucoes', roles: EMPRESA_E_FUNCIONARIO },
    { titulo: 'Funcionários', descricao: 'Equipe com acesso ao sistema.', icone: 'bi-person-workspace', rota: '/funcionarios', roles: SOMENTE_EMPRESA },
    { titulo: 'Parceiros', descricao: 'Postos, hotéis e seguradoras parceiras.', icone: 'bi-building', rota: '/parceiros', roles: SOMENTE_EMPRESA },
    { titulo: 'Cupons', descricao: 'Cupons de desconto para aluguéis.', icone: 'bi-tag', rota: '/cupons', roles: SOMENTE_EMPRESA },
    { titulo: 'Desafios de Cupom', descricao: 'Metas que liberam cupons para clientes.', icone: 'bi-trophy', rota: '/desafios-cupom', roles: SOMENTE_EMPRESA },
    { titulo: 'Taxas e Serviços', descricao: 'Taxas adicionais cobradas no aluguel.', icone: 'bi-cash-coin', rota: '/taxas-servicos', roles: EMPRESA_E_FUNCIONARIO },
    { titulo: 'Monitoramento', descricao: 'Câmeras e leitura automática de placas.', icone: 'bi-camera-video', rota: '/camera-monitor', roles: EMPRESA_E_FUNCIONARIO },
    { titulo: 'Verificação Facial', descricao: 'Cadastro e verificação de identidade.', icone: 'bi-person-bounding-box', rota: '/verificacao-facial', roles: EMPRESA_E_FUNCIONARIO },
    { titulo: 'Configurações', descricao: 'Preço do combustível e parâmetros gerais.', icone: 'bi-gear', rota: '/configuracoes/combustivel', roles: SOMENTE_EMPRESA }
  ];

  constructor(private auth: AuthService) {}

  ngOnInit(): void {
    const usuario = this.auth.getUsuario();
    this.nomeUsuario = usuario?.nomeCompleto ?? '';
    const cargo = usuario?.cargo ?? null;
    this.atalhos = this.todosAtalhos.filter(a => cargoPodeAcessar(cargo, a.roles));
  }
}
