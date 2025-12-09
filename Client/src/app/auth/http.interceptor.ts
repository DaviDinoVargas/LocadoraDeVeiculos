import { HttpInterceptorFn, HttpRequest, HttpHandlerFn, HttpEvent } from '@angular/common/http';
import { Observable } from 'rxjs';

export const DebugInterceptor: HttpInterceptorFn = (req, next) => {
  console.log('Request URL:', req.url);
  console.log('Request Headers:', req.headers);
  console.log('Request Body:', req.body);

  return next(req).pipe(
    // Log da resposta também
    // tap(event => console.log('Response:', event))
  );
};
