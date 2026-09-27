using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using LocadoraDeVeiculos.Core.Dominio.ModuloFuncionario;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloGrupoAutomovel;

public interface IRepositorioGrupoAutomovel : IRepositorio<GrupoAutomovel>
{
    Task<bool> ExisteGrupoComNomeAsync(string nome, Guid? idExcluir = null);
    Task<bool> ExisteAutomovelVinculadoAsync(Guid grupoId);
    Task<bool> ExistePlanoCobrancaVinculadoAsync(Guid grupoId);
}


