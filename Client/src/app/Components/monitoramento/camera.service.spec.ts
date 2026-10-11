import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { CameraService, DeteccaoPlaca } from './camera.service';

const det = (camera: number, plate: string, timestamp: number): DeteccaoPlaca =>
  ({ camera, plate, bbox: [0, 0, 1, 1], timestamp });

describe('CameraService', () => {
  let service: CameraService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    service = TestBed.inject(CameraService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    service.pararTodoMonitoramento();
    http.verify();
  });

  it('monta a URL do stream relativa (via proxy) com o ticket, sem host fixo', () => {
    expect(service.obterStreamUrl(2, 'abc 123')).toBe('/stream/2?ticket=abc%20123');
  });

  it('troca o JWT por um ticket de stream', () => {
    let ticket = '';
    service.obterTicketStream().subscribe(t => (ticket = t));
    const req = http.expectOne('/api/cameras/stream-ticket');
    expect(req.request.method).toBe('POST');
    req.flush({ ticket: 'tk', expiraEmSegundos: 30 });
    expect(ticket).toBe('tk');
  });

  it('não reemite placas que já existiam antes de iniciar a câmera', fakeAsync(() => {
    const recebidas: string[] = [];
    service.placasDetectadas$.subscribe(d => recebidas.push(d.plate));

    service.iniciarMonitoramento(0);
    http.expectOne(r => r.url === '/api/cameras/last').flush({ detections: [det(0, 'VELHA11', 100)] });

    tick(1500);
    const req = http.expectOne(r => r.url === '/api/cameras/last');
    expect(req.request.params.get('since')).toBe('100');
    req.flush({ detections: [det(0, 'VELHA11', 100), det(0, 'NOVA222', 101)] });

    expect(recebidas).toEqual(['NOVA222']);
    service.pararTodoMonitoramento();
  }));

  it('não emite duas vezes a mesma detecção em consultas seguidas', fakeAsync(() => {
    const recebidas: string[] = [];
    service.placasDetectadas$.subscribe(d => recebidas.push(d.plate));
    service.iniciarMonitoramento(0);
    http.expectOne(r => r.url === '/api/cameras/last').flush({ detections: [] });

    tick(1500);
    http.expectOne(r => r.url === '/api/cameras/last').flush({ detections: [det(0, 'AAA1111', 5)] });
    tick(1500);
    http.expectOne(r => r.url === '/api/cameras/last').flush({ detections: [det(0, 'AAA1111', 5)] });

    expect(recebidas).toEqual(['AAA1111']);
    service.pararTodoMonitoramento();
  }));

  it('usa um único temporizador mesmo com várias câmeras e iniciar repetido', fakeAsync(() => {
    service.iniciarMonitoramento(0);
    service.iniciarMonitoramento(0);
    service.iniciarMonitoramento(1);
    http.match(r => r.url === '/api/cameras/last').forEach(r => r.flush({ detections: [] }));

    tick(1500);
    // um único polling => uma única consulta por ciclo
    expect(http.match(r => r.url === '/api/cameras/last').length).toBe(1);
    service.pararTodoMonitoramento();
  }));

  it('para o polling quando a última câmera é parada', fakeAsync(() => {
    service.iniciarMonitoramento(0);
    http.expectOne(r => r.url === '/api/cameras/last').flush({ detections: [] });
    service.pararMonitoramento(0);

    tick(5000);
    http.expectNone(r => r.url === '/api/cameras/last');
  }));

  it('ignora placas de câmeras que não estão sendo monitoradas', fakeAsync(() => {
    const recebidas: string[] = [];
    service.placasDetectadas$.subscribe(d => recebidas.push(d.plate));
    service.iniciarMonitoramento(0);
    http.expectOne(r => r.url === '/api/cameras/last').flush({ detections: [] });

    tick(1500);
    http.expectOne(r => r.url === '/api/cameras/last').flush({ detections: [det(3, 'XXX9999', 9)] });

    expect(recebidas).toEqual([]);
    service.pararTodoMonitoramento();
  }));

  it('erro de rede numa consulta não derruba o polling', fakeAsync(() => {
    const recebidas: string[] = [];
    service.placasDetectadas$.subscribe(d => recebidas.push(d.plate));
    service.iniciarMonitoramento(0);
    http.expectOne(r => r.url === '/api/cameras/last').flush({ detections: [] });

    tick(1500);
    http.expectOne(r => r.url === '/api/cameras/last').flush('falhou', { status: 500, statusText: 'Erro' });
    tick(1500);
    http.expectOne(r => r.url === '/api/cameras/last').flush({ detections: [det(0, 'BBB2222', 7)] });

    expect(recebidas).toEqual(['BBB2222']);
    service.pararTodoMonitoramento();
  }));
});
