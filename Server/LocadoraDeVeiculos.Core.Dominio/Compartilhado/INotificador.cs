using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Dominio.Compartilhado
{
    public interface INotificador
    {
        Task EnviarNotificacaoConfirmacaoAsync(
            string nomePaciente,
            string meioContatoPaciente,
            DateTime inicio,
            DateTime termino
        );
    }
}
