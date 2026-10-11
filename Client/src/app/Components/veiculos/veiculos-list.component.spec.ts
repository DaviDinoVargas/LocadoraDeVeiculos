import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of } from 'rxjs';
import { VeiculosListComponent } from './veiculos-list.component';
import { VeiculosService } from './veiculos.service';
import { GruposVeiculoService } from '../grupoveiculos/grupos-veiculo.service';
import { AuthService } from '../../auth/auth.service';
import { VeiculoDto } from './veiculo.model';

describe('VeiculosListComponent', () => {
  let fixture: ComponentFixture<VeiculosListComponent>;
  let component: VeiculosListComponent;
  let veiculos: jasmine.SpyObj<VeiculosService>;
  let grupos: jasmine.SpyObj<GruposVeiculoService>;

  const economico = { id: 'g1', nome: 'Econômico' };
  const uno = { id: 'v1', placa: 'AAA1A11', marca: 'Fiat', modelo: 'Uno', grupoAutomovelId: 'g1', grupoAutomovelNome: 'Econômico' } as VeiculoDto;
  const hilux = { id: 'v2', placa: 'BBB2B22', marca: 'Toyota', modelo: 'Hilux', grupoAutomovelId: 'g2', grupoAutomovelNome: 'Pickup' } as VeiculoDto;

  const montar = (cargo: string) => {
    veiculos = jasmine.createSpyObj('VeiculosService', ['listar', 'listarPorGrupo', 'excluir']);
    veiculos.listar.and.returnValue(of([uno, hilux]));
    veiculos.listarPorGrupo.and.returnValue(of([uno]));
    grupos = jasmine.createSpyObj('GruposVeiculoService', ['listar']);
    grupos.listar.and.returnValue(of([economico as never]));

    TestBed.configureTestingModule({
      imports: [VeiculosListComponent],
      providers: [
        provideRouter([]),
        { provide: VeiculosService, useValue: veiculos },
        { provide: GruposVeiculoService, useValue: grupos },
        { provide: AuthService, useValue: { getUsuario: () => ({ cargo }) } },
        { provide: MatSnackBar, useValue: { open: () => {} } }
      ]
    });
    fixture = TestBed.createComponent(VeiculosListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  };

  const linhas = () => fixture.nativeElement.querySelectorAll('tbody tr').length;
  const evento = (valor: string) => ({ target: { value: valor } });

  it('filtro por grupo consulta o servidor e a tabela mostra só o resultado (antes o filtro não aparecia na tela)', () => {
    montar('Empresa');
    expect(linhas()).toBe(2);

    component.filtrarPorGrupo(evento('g1'));
    fixture.detectChanges();

    expect(veiculos.listarPorGrupo).toHaveBeenCalledOnceWith('g1');
    expect(linhas()).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('AAA-1A11');
    expect(fixture.nativeElement.textContent).not.toContain('BBB-2B22');
  });

  it('voltar para "Todos os grupos" lista tudo de novo', () => {
    montar('Empresa');
    component.filtrarPorGrupo(evento('g1'));

    component.filtrarPorGrupo(evento(''));
    fixture.detectChanges();

    expect(linhas()).toBe(2);
  });

  it('excluir mantém o filtro de grupo ativo ao recarregar', () => {
    montar('Empresa');
    veiculos.excluir.and.returnValue(of(undefined));
    spyOn(window, 'confirm').and.returnValue(true);
    component.filtrarPorGrupo(evento('g1'));
    veiculos.listarPorGrupo.calls.reset();

    component.excluir('v1');

    expect(veiculos.listarPorGrupo).toHaveBeenCalledWith('g1');
  });

  it('Funcionario: não consulta os grupos (403) e vê o nome do grupo que vem na própria lista', () => {
    montar('Funcionario');

    expect(grupos.listar).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Pickup');
    expect(fixture.nativeElement.textContent).not.toContain('g2');
  });

  it('mostra "—" quando não há nome de grupo nem como resolvê-lo', () => {
    montar('Funcionario');

    expect(component.obterNomeGrupo({ ...uno, grupoAutomovelNome: undefined })).toBe('—');
  });
});
