import { Component, HostListener, OnInit, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { MatIcon } from '@angular/material/icon';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { AuthService } from '../../auth/auth.service';

interface MenuItem {
  label: string;
  icon: string;
  path: string;
  roles: string[];
  module?: string;
  children?: MenuItem[];
  expanded?: boolean; // Para controlar expansão
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
        label: 'Dashboard',
        icon: 'dashboard',
        path: '/dashboard',
        roles: ['Empresa', 'Funcionario'],
        module: this.modules.dashboard
      },
      {
        label: 'Home',
        icon: 'home',
        path: '/home',
        roles: ['Empresa', 'Funcionario'],
        module: this.modules.dashboard
      },

      // Gestão
      {
        label: 'Funcionários',
        icon: 'people',
        path: '/funcionarios',
        roles: ['Empresa'],
        module: this.modules.gestao
      },
      {
        label: 'Clientes',
        icon: 'person',
        path: '/clientes',
        roles: ['Empresa', 'Funcionario'],
        module: this.modules.gestao,
        expanded: false,
        children: [
          { label: 'Pessoas Físicas', icon: 'person_outline', path: '/clientes/pf', roles: ['Empresa', 'Funcionario'] },
          { label: 'Pessoas Jurídicas', icon: 'business', path: '/clientes/pj', roles: ['Empresa', 'Funcionario'] }
        ]
      },
      {
        label: 'Condutores',
        icon: 'drive_eta',
        path: '/condutores',
        roles: ['Empresa', 'Funcionario'],
        module: this.modules.gestao
      },

      // Veículos
      {
        label: 'Veículos',
        icon: 'directions_car',
        path: '/veiculos',
        roles: ['Empresa', 'Funcionario'],
        module: this.modules.gestao,
        expanded: false,
        children: [
          { label: 'Todos Veículos', icon: 'car_rental', path: '/veiculos', roles: ['Empresa', 'Funcionario'] },
          { label: 'Grupos', icon: 'category', path: '/grupoveiculos', roles: ['Empresa'] },
          { label: 'Planos', icon: 'attach_money', path: '/planos-cobranca', roles: ['Empresa'] }
        ]
      },

      // Operacional
      {
        label: 'Aluguéis',
        icon: 'receipt_long',
        path: '/alugueis',
        roles: ['Empresa', 'Funcionario'],
        module: this.modules.operacional,
        expanded: false,
        children: [
          { label: 'Novo Aluguel', icon: 'add_circle', path: '/alugueis/novo', roles: ['Empresa', 'Funcionario'] },
          { label: 'Em Aberto', icon: 'pending_actions', path: '/alugueis/abertos', roles: ['Empresa', 'Funcionario'] },
          { label: 'Todos', icon: 'list', path: '/alugueis', roles: ['Empresa', 'Funcionario'] }
        ]
      },
      {
        label: 'Devoluções',
        icon: 'assignment_returned',
        path: '/devolucoes',
        roles: ['Empresa', 'Funcionario'],
        module: this.modules.operacional
      },

      // Financeiro
      {
        label: 'Taxas e Serviços',
        icon: 'request_quote',
        path: '/taxas-servicos',
        roles: ['Empresa', 'Funcionario'],
        module: this.modules.financeiro
      },

      // Configurações
      {
        label: 'Configurações',
        icon: 'settings',
        path: '/configuracoes',
        roles: ['Empresa'],
        module: this.modules.configuracoes,
        expanded: false,
        children: [
          { label: 'Preço Combustível', icon: 'local_gas_station', path: '/configuracoes/combustivel', roles: ['Empresa'] }
        ]
      }
    ];

    this.menuItems = allMenuItems.filter(item =>
      this.hasPermission(item.roles) &&
      (!item.children || item.children.some(child => this.hasPermission(child.roles)))
    );
  }

  private hasPermission(requiredRoles: string[]): boolean {
    if (!this.userRole) return false;
    return requiredRoles.includes(this.userRole);
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
    this.router.navigate([path]).then(() => {
      this.onNavigateMobile();
    });
  }

  isActive(path: string): boolean {
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
