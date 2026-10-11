import { NotificacaoService } from '../notificacao.service';

/** Dublê do NotificacaoService para os specs: os avisos são espiões que não mostram toast. */
export function criarNotificacaoFalsa(): jasmine.SpyObj<NotificacaoService> {
  return jasmine.createSpyObj<NotificacaoService>('NotificacaoService', ['sucesso', 'erro', 'aviso', 'info']);
}
