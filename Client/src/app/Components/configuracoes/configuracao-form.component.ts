import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ConfiguracoesService } from './configuracoes.service';
import { ConfiguracaoDto } from './configuracao.model';
import { CommonModule } from '@angular/common';
import { Observable, concat, last, of } from 'rxjs';

@Component({
  selector: 'app-configuracao-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './configuracao-form.component.html',
  styleUrls: ['../scss/global.scss']
})
export class ConfiguracaoFormComponent implements OnInit {
  form!: FormGroup;
  loading = false;
  serverErrors: string[] = [];
  mensagemSucesso: string = '';
  // Valores carregados do servidor: só chamamos o PUT do que realmente mudou.
  private original: ConfiguracaoDto | null = null;

  constructor(
    private fb: FormBuilder,
    private svc: ConfiguracoesService,
    private router: Router,
    private snack: MatSnackBar
  ) {
    this.form = this.fb.group({
      precoCombustivel: [0, [Validators.required, Validators.min(0.01)]],
      valorGarantia: [0, [Validators.required, Validators.min(0), Validators.max(100000)]]
    });
  }

  ngOnInit() {
    this.carregar();
  }

  carregar() {
    this.loading = true;
    this.svc.obter().subscribe({
      next: config => {
        this.original = config;
        this.form.patchValue(config);
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        console.error('Erro ao carregar configurações:', err);
        this.snack.open('Erro ao carregar configurações', 'Fechar', { duration: 4000 });
      }
    });
  }

  salvar() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.snack.open('Verifique os campos obrigatórios', 'Fechar', { duration: 3500 });
      return;
    }

    const { precoCombustivel, valorGarantia } = this.form.getRawValue();
    this.loading = true;
    this.serverErrors = [];
    this.mensagemSucesso = '';

    // Em sequência (e não em paralelo): os dois PUTs leem e gravam a mesma linha de
    // configuração, e se ela ainda não existir os dois tentariam criá-la.
    const chamadas: Observable<unknown>[] = [];
    if (precoCombustivel !== this.original?.precoCombustivel) {
      chamadas.push(this.svc.atualizarPrecoCombustivel(precoCombustivel));
    }
    if (valorGarantia !== this.original?.valorGarantia) {
      chamadas.push(this.svc.atualizarValorGarantia(valorGarantia));
    }
    const salvar$ = chamadas.length ? concat(...chamadas).pipe(last()) : of(null);

    salvar$.subscribe({
      next: () => {
        this.original = { ...this.original, precoCombustivel, valorGarantia };
        this.loading = false;
        this.mensagemSucesso = 'Configurações atualizadas com sucesso!';
        this.snack.open('Configurações atualizadas', 'Fechar', { duration: 3000 });
      },
      error: (err: any) => {
        this.loading = false;
        this.serverErrors = [err?.message ?? 'Erro desconhecido'];
        this.snack.open(this.serverErrors[0], 'Fechar', { duration: 4000 });
      }
    });
  }

  cancelar() {
    this.router.navigate(['/home']);
  }
}
