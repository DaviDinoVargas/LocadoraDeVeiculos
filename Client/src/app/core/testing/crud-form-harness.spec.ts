import { Type, Provider } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { NotificacaoService } from '../notificacao.service';
import { criarNotificacaoFalsa } from './notificacao-fake.spec';

/**
 * Monta um formulário de cadastro/edição feito com `crudForm()` com a rota (`:id` opcional), o roteador e
 * os avisos já dublados. Cada spec só informa os serviços do formulário em `providers`.
 */
export function montarFormCrud<T>(tipo: Type<T>, opcoes: { id?: string | null; providers: Provider[] }) {
  const notificacao = criarNotificacaoFalsa();
  const params = new BehaviorSubject(convertToParamMap(opcoes.id ? { id: opcoes.id } : {}));

  TestBed.configureTestingModule({
    imports: [tipo],
    providers: [
      provideRouter([]),
      { provide: NotificacaoService, useValue: notificacao },
      { provide: ActivatedRoute, useValue: { paramMap: params, snapshot: { paramMap: params.value } } },
      ...opcoes.providers
    ]
  });
  const router = TestBed.inject(Router);
  spyOn(router, 'navigate').and.resolveTo(true);
  const fixture: ComponentFixture<T> = TestBed.createComponent(tipo);
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance, router, notificacao };
}
