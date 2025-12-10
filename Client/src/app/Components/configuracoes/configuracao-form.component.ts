import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ConfiguracoesService } from './configuracoes.service';
import { ConfiguracaoDto } from './configuracao.model';
import { CommonModule } from '@angular/common';

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

  constructor(
    private fb: FormBuilder,
    private svc: ConfiguracoesService,
    private router: Router,
    private snack: MatSnackBar
  ) {
    this.form = this.fb.group({
      precoCombustivel: [0, [Validators.required, Validators.min(0)]]
    });
  }

  ngOnInit() {
    this.carregar();
  }

  carregar() {
    this.loading = true;
    this.svc.obter().subscribe({
      next: config => {
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

    const precoCombustivel = this.form.get('precoCombustivel')?.value;
    this.loading = true;
    this.serverErrors = [];
    this.mensagemSucesso = '';

    this.svc.atualizarPrecoCombustivel(precoCombustivel).subscribe({
      next: () => {
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
