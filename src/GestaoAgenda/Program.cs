using GestaoAgenda.Data;
using GestaoAgenda.Models;
using GestaoAgenda.Repositories;
using GestaoAgenda.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddScoped<IClienteRepository, ClienteRepository>();
builder.Services.AddScoped<IServicoRepository, ServicoRepository>();
builder.Services.AddScoped<IAgendamentoRepository, AgendamentoRepository>();

builder.Services.AddDbContext<GestaoAgendaContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

var cultureInfo = new CultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;


// ======================================================
// DADOS INICIAIS PARA DEMONSTRAÇÃO
// ======================================================

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<GestaoAgendaContext>();

    // CLIENTES
    if (!await db.Clientes.AnyAsync())
    {
        db.Clientes.AddRange(
            new Cliente
            {
                Nome = "Ana Paula Souza",
                Telefone = "(11) 98765-4321",
                Email = "ana.souza@exemplo.com"
            },
            new Cliente
            {
                Nome = "Juliana Martins",
                Telefone = "(11) 97654-3210",
                Email = "juliana.martins@exemplo.com"
            },
            new Cliente
            {
                Nome = "Fernanda Oliveira",
                Telefone = "(11) 96543-2109",
                Email = "fernanda.oliveira@exemplo.com"
            },
            new Cliente
            {
                Nome = "Mariana Santos",
                Telefone = "(11) 95432-1098",
                Email = "mariana.santos@exemplo.com"
            },
            new Cliente
            {
                Nome = "Carla Rodrigues",
                Telefone = "(11) 94321-0987",
                Email = "carla.rodrigues@exemplo.com"
            }
        );

        await db.SaveChangesAsync();
    }


    // SERVIÇOS
    if (!await db.Servicos.AnyAsync())
    {
        db.Servicos.AddRange(
            new Servico
            {
                Nome = "Corte Feminino",
                Descricao = "Corte, lavagem e finalização",
                Duracao = 60,
                Valor = 70.00m
            },
            new Servico
            {
                Nome = "Escova",
                Descricao = "Lavagem e escova modeladora",
                Duracao = 45,
                Valor = 50.00m
            },
            new Servico
            {
                Nome = "Manicure",
                Descricao = "Cuidado das unhas e esmaltação",
                Duracao = 40,
                Valor = 35.00m
            }
        );

        await db.SaveChangesAsync();
    }
}

app.Run();
