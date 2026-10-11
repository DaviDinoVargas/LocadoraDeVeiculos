import { Component, HostListener, OnInit, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { MatIcon } from '@angular/material/icon';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { AuthService } from '../../auth/auth.service';
import { EMPRESA_E_FUNCIONARIO, SOMENTE_EMPRESA, cargoPodeAcessar } from '../../core/permissoes';

interface MenuItem {
  label: string;
  icon: string;
  path: string;
  roles: readonly string[];
  module?: string;
  children?: MenuItem[];
  expanded?: boolean;
}

@Component({
  selector: 'app-sidebar',
  templateUrl: './sidebar.component.html',
  styleUrls: ['./sidebar.component.scss'],
  standalone: true,
  imports: [MatIcon, CommonModule]
})
export class SidebarComponent implements OnInit, OnDestroy {
  isMobile = false;
  mobileOpen = false;
  collapsed = false;
  userRole: string | null = null;
  private authSubscription?: Subscription;

  readonly width = 260;
  readonly collapsedWidth = 64;

  menuItems: MenuItem[] = [];

  modules = {
    dashboard: 'Dashboard',
    gestao: 'Gestão',
    operacional: 'Operacional',
    financeiro: 'Financeiro',
    configuracoes: 'Configurações'
  };

  constructor(
    private router: Router,
    private authService: AuthService
  ) {
    this.checkMobile();
  }

  ngOnInit() {
    this.checkMobile();
    const usuario = this.authService.getUsuario();
    this.userRole = usuario?.cargo ?? null;
    this.updateMenuItems();
  }

  ngOnDestroy() {
    this.authSubscription?.unsubscribe();
  }

  @HostListener('window:resize')
  onResize() {
    this.checkMobile();
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent) {
    if (this.isMobile && this.mobileOpen) {
      const sidebar = document.querySelector('.sidebar');
      const hamburger = document.querySelector('.hamburger');

      if (sidebar && !sidebar.contains(event.target as Node) &&
          hamburger && !hamburger.contains(event.target as Node)) {
        this.closeMobile();
      }
    }
  }

  private checkMobile() {
    this.isMobile = window.innerWidth <= 800;
    if (!this.isMobile) {
      this.mobileOpen = false;
      document.body.style.overflow = '';
    }
  }

  private updateMenuItems() {
    const allMenuItems: MenuItem[] = [
      // Dashboard
      {
        label: 'Home',
        icon: 'home',
        path: '/home',
        roles: EMPRESA_E_FUNCIONARIO,
        module: this.modules.dashboard
      },

      // Gestão
      {
        label: 'Funcionários',
        icon: 'people',
        path: '/funcionarios',
        roles: SOMENTE_EMPRESA,
        module: this.modules.gestao
      },
      {
        label: 'Clientes',
        icon: 'person',
        path: '/clientes',
        roles: EMPRESA_E_FUNCIONARIO,
        module: this.modules.gestao,
        expanded: false,
        children: [
          { label: 'Todos os Clientes', icon: 'people_outline', path: '/clientes', roles: EMPRESA_E_FUNCIONARIO },
          { label: 'Pessoas Físicas', icon: 'person_outline', path: '/clientes/pf', roles: EMPRESA_E_FUNCIONARIO },
          { label: 'Pessoas Jurídicas', icon: 'business', path: '/clientes/pj', roles: EMPRESA_E_FUNCIONARIO }
        ]
      },
      {
        label: 'Condutores',
        icon: 'drive_eta',
        path: '/condutores',
        roles: EMPRESA_E_FUNCIONARIO,
        module: this.modules.gestao
      },

      // Veículos
      {
        label: 'Veículos',
        icon: 'directions_car',
        path: '/veiculos',
        roles: EMPRESA_E_FUNCIONARIO,
        module: this.modules.gestao,
        expanded: false,
        children: [
          { label: 'Todos Veículos', icon: 'car_rental', path: '/veiculos', roles: EMPRESA_E_FUNCIONARIO },
          { label: 'Grupos', icon: 'category', path: '/grupoveiculos', roles: SOMENTE_EMPRESA },
          { label: 'Planos', icon: 'attach_money', path: '/planos-cobranca', roles: SOMENTE_EMPRESA }
        ]
      },

      // Operacional
      {
        label: 'Aluguéis',
        icon: 'receipt_long',
        path: '/alugueis',
        roles: EMPRESA_E_FUNCIONARIO,
        module: this.modules.operacional,
        expanded: false,
        children: [
          { label: 'Novo Aluguel', icon: 'add_circle', path: '/alugueis/new', roles: EMPRESA_E_FUNCIONARIO },
          { label: 'Em Aberto', icon: 'pending_actions', path: '/alugueis?status=em-aberto', roles: EMPRESA_E_FUNCIONARIO },
          { label: 'Todos', icon: 'list', path: '/alugueis', roles: EMPRESA_E_FUNCIONARIO }
        ]
      },
      {
  label: 'Devoluções',
  icon: 'assignment_returned',
  path: '/devolucoes',
  roles: EMPRESA_E_FUNCIONARIO,
  module: this.modules.operacional
},

      // Financeiro
      {
        label: 'Taxas e Serviços',
        icon: 'request_quote',
        path: '/taxas-servicos',
        roles: EMPRESA_E_FUNCIONARIO,
        module: this.modules.financeiro
      },
      {
        label: 'Parceiros',
        icon: 'business',
        path: '/parceiros',
        roles: SOMENTE_EMPRESA,
        module: this.modules.financeiro
      },
      {
        label: 'Cupons',
        icon: 'local_offer',
        path: '/cupons',
        roles: SOMENTE_EMPRESA,
        module: this.modules.financeiro,
        expanded: false,
        children: [
          { label: 'Todos os Cupons', icon: 'list', path: '/cupons', roles: SOMENTE_EMPRESA },
          { label: 'Desafios de Cupom', icon: 'emoji_events', path: '/desafios-cupom', roles: SOMENTE_EMPRESA }
        ]
      },

      // Configurações
      {
        label: 'Configurações',
        icon: 'settings',
        path: '/configuracoes',
        roles: SOMENTE_EMPRESA,
        module: this.modules.configuracoes,
        expanded: false,
        children: [
          { label: 'Preço Combustível', icon: 'local_gas_station', path: '/configuracoes/combustivel', roles: SOMENTE_EMPRESA }
        ]
      },

      {
  label: 'Monitoramento',
  icon: 'videocam',
  path: '/camera-monitor',
  roles: EMPRESA_E_FUNCIONARIO,
  module: this.modules.operacional
},

      {
  label: 'Verificação Facial',
  icon: 'face',
  path: '/verificacao-facial',
  roles: EMPRESA_E_FUNCIONARIO,
  module: this.modules.operacional
}
    ];

    // Filtra também os subitens: antes um Funcionario via "Grupos" e "Planos" (só Empresa)
    // dentro de Veículos e tomava 403 ao clicar.
    this.menuItems = allMenuItems
      .filter(item => this.hasPermission(item.roles))
      .map(item => item.children
        ? { ...item, children: item.children.filter(child => this.hasPermission(child.roles)) }
        : item)
      .filter(item => !item.children || item.children.length > 0);
  }

  private hasPermission(requiredRoles: readonly string[]): boolean {
    return cargoPodeAcessar(this.userRole, requiredRoles);
  }

  // Métodos de navegação
  openMobile() {
    this.mobileOpen = true;
    document.body.style.overflow = 'hidden';
  }

  closeMobile() {
    this.mobileOpen = false;
    document.body.style.overflow = '';
  }

  toggleCollapse() {
    if (!this.isMobile) {
      this.collapsed = !this.collapsed;
    }
  }

  onNavigateMobile() {
    if (this.isMobile) {
      this.closeMobile();
    }
  }

  navigate(path: string, event?: Event) {
    if (event) {
      event.preventDefault();
      event.stopPropagation();
    }

    this.router.navigateByUrl(path).then(() => {
      this.onNavigateMobile();
    }).catch(error => {
      console.error('Erro na navegação:', error);
    });
  }

  isActive(path: string): boolean {
    // Verifica se a rota atual começa com o path fornecido
    // Para lidar com /clientes, /clientes/pf, /clientes/pj
    return this.router.url.startsWith(path);
  }

  hasChildren(item: MenuItem): boolean {
    return !!item.children && item.children.length > 0;
  }

  isChildActive(item: MenuItem): boolean {
    if (!item.children) return false;
    return item.children.some(child => this.isActive(child.path));
  }

  toggleSubMenu(event: Event, item: MenuItem) {
    if (this.hasChildren(item)) {
      event.preventDefault();
      event.stopPropagation();
      item.expanded = !item.expanded;
    }
  }

  navigateParent(event: Event, item: MenuItem) {
    if (this.hasChildren(item)) {
      this.toggleSubMenu(event, item);
    } else {
      this.navigate(item.path, event);
    }
  }

  // Novo método para toggle no mobile
  toggleMobile() {
    if (this.isMobile) {
      this.mobileOpen ? this.closeMobile() : this.openMobile();
    } else {
      this.toggleCollapse();
    }
  }
}
