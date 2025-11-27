using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.Identify
{
    public static class RoleSeeder
    {
        public static async Task CriarCargosAsync(RoleManager<Cargo> roleManager)
        {
            string[] cargos = { "usuario", "empresa", "administrador" };

            foreach (var cargo in cargos)
            {
                bool existe = await roleManager.RoleExistsAsync(cargo);

                if (!existe)
                {
                    await roleManager.CreateAsync(new Cargo
                    {
                        Name = cargo,
                        NormalizedName = cargo.ToUpper()
                    });
                }
            }
        }
    }
}