# Etapa E — revisão final de profissionais, serviços e agendamentos

## Resultado

Validação automatizada concluída: restore aprovado, build Release com zero avisos
e zero erros, 161 testes aprovados, nenhum ignorado ou com falha.
A funcionalidade está implementada nas etapas B/C/D. A homologação visual em
navegador/celular e os testes de gravação/concorrência em SQL Server isolado continuam
pendentes; não se afirma que esses cenários foram executados.

Nenhum dado ou esquema de banco real foi modificado pelo assistente nesta etapa.
Não houve aplicação/criação de migration, commit, push ou merge. Alterações
preexistentes e implementações das etapas anteriores foram preservadas.

## Comandos executados

Na raiz real do repositório:

    dotnet restore ProjetoIntegradorAgenda.slnx
    dotnet build ProjetoIntegradorAgenda.slnx --configuration Release --no-restore
    dotnet test ProjetoIntegradorAgenda.slnx --configuration Release --no-build --no-restore
    dotnet ef migrations has-pending-model-changes --project src/GestaoAgenda --configuration Release --no-build
    git diff --check

Resultados:
- Restore: todas as dependências disponíveis.
- Build: aprovado, zero avisos/erros.
- Testes: 161 aprovados.
- Modelo: sem diferenças pendentes desde a última migration.
- Diff: sem erros de whitespace; Git informa normalização LF/CRLF em alguns arquivos.

A baseline desta etapa era de 152 testes aprovados. Foram acrescentados oito testes
JavaScript do agendamento e um teste da mensagem de exclusão de serviço.

## Correções realizadas nesta revisão

1. Limpar o serviço durante uma busca pendente podia deixar o select de profissional
   desabilitado. O novo teste reproduziu a falha; a correção restaura o campo e o
   estado permitido do botão, sem aceitar respostas antigas.
2. A mensagem de falha ao excluir serviço citava apenas agendamentos, embora agora
   uma associação com profissional também impeça exclusão. O texto passou a orientar
   sobre ambos os vínculos, sem modificar o comportamento de exclusão.
3. A documentação foi atualizada com o estado real observado do histórico EF.

O erro de preço relatado antes desta etapa já foi corrigido: pontos de milhar
são tratados como literais, não como qualquer caractere. Os testes que executam
o JavaScript real continuam aprovados para 40,00, 40,50, 1.234,56 e limites monetários.

## Matriz dos requisitos obrigatórios

| Requisito | Evidência automatizada | Limite |
|---|---|---|
| Associação válida | ProfissionalServicoTests: cadastro com várias ofertas e mesmo serviço em vários profissionais | Persistência exercitada com EF InMemory |
| Duplicidades | Rejeição na entrada, reenvio/reativação e teste do índice único | Índice também confirmado no SQL; não houve INSERT de colisão no banco real |
| Preço inválido | ProfissionalFormularioTests, ProfissionalPrecoJavascriptTests e HTTP | Motor JS com DOM simulado, não navegador |
| Duração inválida | ProfissionalServicoTests e POSTs HTTP manipulados | Limites 1 a 1440; CHECK confirmado por metadados SQL |
| Filtro por serviço | Consulta de elegibilidade e endpoint HTTP | Sem N+1 nos caminhos examinados |
| Profissional inativo | Testes de serviço/endpoint e bloqueio entre GET e POST | Sem mecanismo de autenticação novo |
| Associação inativa | Testes de serviço, endpoint e revalidação no POST | Serviço global não possui flag Ativo; o status configurado é o da oferta |
| Duração/término | AgendamentoServiceTests e AgendamentoJavascriptTests | Inclui meia-noite e limite de data |
| Conflitos | Intervalos adjacentes/sobrepostos, agenda global e solicitações concorrentes | Concorrência validada na mesma instância com EF InMemory/TestServer |
| Histórico após mudar preço | Snapshots preservados ao manter oferta; troca explícita adota valores atuais | Não houve backfill de registros antigos |
| Integridade após desativação | Metadados/observações preservados e reagendamento/reativação recusados quando inelegíveis | FKs sem cascata confirmadas no SQL; não foi feito DELETE real |

Cobertura adicional:
- Antiforgery real no pipeline MVC e rejeição de campos de saída/grafos no POST.
- Formulário incompleto ou catálogo indisponível não inativa ofertas silenciosamente.
- Falha de gravação preserva formulário e permite nova tentativa.
- Reenvio do profissional não duplica cadastro na mesma instância.
- Fallback de carregamento dos profissionais sem JavaScript, sem gravar reserva.
- Lista, detalhes e semana renderizam profissional e dados desconhecidos sem inventá-los.
- Reservas antigas sem duração bloqueiam os dias afetados pela regra conservadora.
- JavaScript do agendamento testa respostas fora de ordem, erro HTTP, ausência de
  elegíveis, opções históricas e formulário bloqueado no servidor.
- Resumo da reserva mantém snapshots e recalcula ao escolher outra oferta.
- Somente preço e duração da oferta são alterados; padrões globais permanecem intactos.

## Auditoria somente de leitura do SQL Server configurado

Foi possível abrir a conexão configurada e consultar apenas catálogos de sistema
e __EFMigrationsHistory. Nenhuma credencial ou dado de cliente foi incluído no relatório.
Não se iniciou Program.cs nem sua carga demonstrativa.

Constatações:
- Profissional e ProfissionalServico existem.
- ProfissionalServico possui preço decimal(18,2), duração int e Ativo bit.
- Agendamento contém ProfissionalServicoId, PrecoAplicado decimal(18,2) e
  DuracaoMinutosAplicada, todos aceitando NULL.
- O índice único IX_ProfissionalServico_ProfissionalId_ServicoId existe e suas
  chaves, nessa ordem, são ProfissionalId e ServicoId.
- O índice global UX_Agendamento_Data_Hora_Ativo existe sobre Data/Hora, único
  e filtrado para status diferente de Cancelado.
- As novas FKs e as legadas de Agendamento estão habilitadas, confiáveis e
  configuradas como NO_ACTION (sem exclusão em cascata).
- Os quatro CHECKs de preço/duração estão habilitados e confiáveis; os de
  Agendamento aceitam NULL.
- O histórico EF já contém:
  - 20261029235831_AdicionarProfissionais
  - 20261030002109_AssociarServicosAProfissionais

Esses registros e objetos já estavam presentes quando foram consultados.
Não foram aplicados pelo assistente. A constatação substitui as notas anteriores
de aplicação pendente, que descreviam o estado conhecido nas etapas anteriores.

Diferenças legadas:
- PK/FKs de Agendamento e o índice único global usam nomes do script SQL original,
  diferentes das convenções registradas no snapshot.
- A consulta não retornou os índices IX_Agendamento_ClienteId e
  IX_Agendamento_ServicoId previstos pelas convenções do modelo.
- Nenhum desses objetos foi renomeado/criado automaticamente.
- Futuras migrations que alterem objetos legados precisam revisar os nomes reais.
- Esta auditoria não equivale a comparação integral de collation, defaults e
  todos os objetos do banco, nem comprova o comportamento de transações/retries.

## Arquivos desta Etapa E

Criados:
- tests/GestaoAgenda.Tests/AgendamentoJavascriptTests.cs
- tests/GestaoAgenda.Tests/ServicoExclusaoTests.cs
- docs/revisao-final-profissionais-servicos.md

Modificados:
- src/GestaoAgenda/wwwroot/js/agendamento-ofertas.js: recuperação do select.
- src/GestaoAgenda/Controllers/ServicoController.cs: mensagem sobre vínculos.
- Documentos das etapas anteriores: nota de verificação atualizada do banco.

## Inventário da implementação acumulada

Entidades/mapeamento:
- Models/ProfissionalServico.cs e Models/IntervaloAgendamento.cs.
- Profissional, Servico, Agendamento e GestaoAgendaContext atualizados.
- Migration AssociarServicosAProfissionais, designer e snapshot.
- Script incremental docs/sql-server/associar-servicos-profissionais.sql.

Aplicação:
- Services/ProfissionalServicoService.cs.
- Services/AgendamentoService.cs.
- Services/EnviosFormularioProfissional.cs.
- ViewModels/ProfissionalFormViewModel.cs e AgendamentoFormViewModel.cs.
- ProfissionalController, AgendamentoController e registros de DI em Program.cs.
- Consultas de AgendamentoRepository com profissional e sem tracking.

Interface:
- Views/Profissional: formulário, ofertas, cadastro, edição, detalhes e scripts.
- Views/Agendamento: formulário, cadastro, edição, detalhes, lista, semana e scripts.
- wwwroot/js/profissional-servicos.js e agendamento-ofertas.js.

Testes:
- Validação de entidades e formulários, repositórios, serviços, metadados,
  migrations, HTTP MVC/antiforgery, renderização Razor e JavaScript.
- Dependências adicionais somente de testes: EF InMemory, TestHost, Jint.

## Pendências para homologação

1. Executar CRUD, unicidade, FKs, rollback, retries e reservas concorrentes entre
   instâncias em SQL Server ISOLADO. Isso requer preparar um banco de teste com
   autorização; não usar o banco configurado como alvo de testes de escrita.
2. Inspecionar em navegador/celular: 320, 360, 390 px e desktop, teclado, rolagem,
   mensagens, foco, valor monetário e seleção dinâmica.
   A tentativa de Computer Use falhou antes de iniciar, com
   CreateProcessSecurityEnvironment / HRESULT 0x80070020. Não foram capturadas telas.
3. Rever os objetos legados antes de uma futura alteração de esquema.
4. Considerar armazenamento compartilhado de idempotência se houver múltiplas
   instâncias: os tokens de reenvio dos profissionais estão na memória local.

Limitações preexistentes fora do escopo:
- Não há autenticação/autorização por usuário ou estabelecimento implementada.
- O script global site.js tenta inicializar DataTables sem o núcleo carregado.
  As novas interações usam scripts próprios, mas a pendência global permanece.
- A inicialização tem seed demonstrativo; testes não executam esse Program.cs.
- Não foi introduzida desativação global de Servico nem um fluxo de revisão
  retroativa de preço/duração históricos.