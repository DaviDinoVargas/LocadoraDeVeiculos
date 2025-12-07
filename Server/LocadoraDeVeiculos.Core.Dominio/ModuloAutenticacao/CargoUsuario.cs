using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;

public enum CargoUsuario
{
    Empresa,
    [Display(Name = "Funcionário")] Funcionario
}
