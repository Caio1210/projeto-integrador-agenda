# Projeto Integrador - Agenda do Salão

Sistema web em ASP.NET Core MVC para controle de agenda de um pequeno salão de beleza.

## Funcionalidades
- Dashboard com resumo da agenda
- Cadastro de clientes
- Cadastro de serviços
- Cadastro, edição, consulta e exclusão de agendamentos
- Controle de status: Agendado, Concluído e Cancelado
- Bloqueio de dois agendamentos ativos no mesmo dia e horário
- SQL Server com Entity Framework Core

## Execução
1. Instale o .NET 10 SDK.
2. Instale e execute o SQL Server.
3. Confira a conexão em `src/GestaoAgenda/appsettings.json`.
4. No terminal, entre em `src/GestaoAgenda`.
5. Execute `dotnet run`.
