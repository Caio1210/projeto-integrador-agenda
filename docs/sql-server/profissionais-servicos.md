# Profissionais e serviços — Etapa B

## Implementado no código

- ProfissionalServico com Id identity, ProfissionalId, ServicoId, Preco,
  DuracaoMinutos e Ativo.
- Navegações em Profissional e Servico.
- Preço decimal(18,2), maior que zero, com até duas casas decimais; duração
  entre 1 e 1440 minutos.
- Índice único (ProfissionalId, ServicoId), incluindo ofertas inativas.
- FKs com DeleteBehavior.Restrict; sem exclusão física de associações.
- Agendamento com ProfissionalServicoId, PrecoAplicado e
  DuracaoMinutosAplicada opcionais para manter compatibilidade dos registros legados.

ProfissionalServicoService usa o DbContext injetado para o caso de uso que grava
profissional e ofertas juntos. Os repositórios existentes continuam atendendo os
fluxos de leitura atuais; os formulários foram integrados ao serviço na Etapa C.
As operações de leitura usam projeção ou Include, sem consultas por item.

SalvarAsync recebe os dados do profissional e a lista completa de ofertas. Ausência
de uma oferta previamente cadastrada significa inativação, não exclusão.
Reenvios de configuração para o mesmo profissional e serviço e reativações atualizam
a mesma linha. Não significa idempotência da criação de um novo profissional:
a proteção do envio do formulário foi implementada na Etapa C.

Todas as entradas são validadas antes de modificar o grafo rastreado. Profissional
e ofertas usam um único SaveChangesAsync, com atomicidade transacional padrão do
SQL Server. Conflitos de índice ou FKs recebem mensagem de falha sem expor detalhes
do banco; o erro técnico é registrado no log. A recuperação da falha restaura o
estado das entidades envolvidas e permite nova tentativa.

O serviço nunca altera Servico.Valor, Servico.Duracao ou os agendamentos existentes.
O filtro de elegibilidade requer serviço correspondente, associação ativa e
profissional ativo. Ele está pronto para a integração da Etapa D.

## Migration criada e script gerado — aplicação pendente

Tabela nova: ProfissionalServico.

| Coluna | Tipo | Nulo |
|---|---|---|
| Id | int IDENTITY | não |
| ProfissionalId | int, FK Profissional | não |
| ServicoId | int, FK Servico | não |
| Preco | decimal(18,2) | não |
| DuracaoMinutos | int | não |
| Ativo | bit | não |

Adicionar a Agendamento:

| Coluna | Tipo | Nulo |
|---|---|---|
| ProfissionalServicoId | int, FK ProfissionalServico | sim |
| PrecoAplicado | decimal(18,2) | sim |
| DuracaoMinutosAplicada | int | sim |

Índice único no par de profissional/serviço e índices de FKs por convenção.
CHECK de preço positivo e duração entre 1 e 1440 nas ofertas; nos snapshots,
os CHECKs aceitam NULL para preservar os registros legados.
Todas as novas FKs usam exclusão restrita.

Não há preenchimento retroativo, default numérico ou alteração do índice global
de conflitos (Data, Hora). A migration AdicionarProfissionais foi preservada.
Após autorização, foi criada AssociarServicosAProfissionais, com seu designer,
e o snapshot foi atualizado para o modelo atual.

O script incremental idempotente está em associar-servicos-profissionais.sql.
Ele contém apenas a evolução posterior a AdicionarProfissionais: assume que a
migration anterior já está aplicada e registrada em __EFMigrationsHistory.
Não cria Profissional nem inicializa um banco vazio. A idempotência depende do
histórico EF; não substitui a conferência do esquema real.

Para reproduzir a geração (não aplica ao banco):

    dotnet tool restore
    dotnet build src/GestaoAgenda/GestaoAgenda.csproj --configuration Release
    dotnet ef migrations script AdicionarProfissionais AssociarServicosAProfissionais --idempotent --project src/GestaoAgenda --configuration Release --no-build --output docs/sql-server/associar-servicos-profissionais.sql

A aplicação exige autorização separada e conferência do destino.
Não foi executado database update, Migrate, EnsureCreated nem DDL/DML no SQL Server.
O método Down foi gerado, mas não executado: ele remove a tabela associativa e as
três novas colunas, perdendo seus dados. Reversão exige avaliação e autorização próprias.

Antes da aplicação, comparar o esquema real e __EFMigrationsHistory com o histórico
existente. A migration anterior assume Cliente, Servico e Agendamento preexistentes.
Há diferenças documentadas entre nomes de índices/FKs no script original e no
snapshot; não tentar recriar nem renomear esses objetos automaticamente.

## Estado intermediário e próximas etapas

Este código não deve ser publicado com o banco antigo: consultas a Agendamento
passam a referenciar as novas colunas. A atualização de esquema autorizada deve
preceder a ativação da versão final em ambiente de teste.

Etapa C: ViewModels, seção Serviços oferecidos no cadastro/edição, preços pt-BR,
mensagens e prevenção de reenvio.
Etapa D: selecionar associação elegível, gravar snapshots, calcular término e
verificar sobreposição global com controle de concorrência.
O fluxo HTTP atual de reserva ainda não foi adaptado nesta etapa.
Os registros legados permanecem com valores desconhecidos, sem preços ou durações
inventados. A política conservadora aprovada para eles será implementada na Etapa D.

## Validação

Na raiz:

    dotnet restore ProjetoIntegradorAgenda.slnx
    dotnet build ProjetoIntegradorAgenda.slnx --configuration Release --no-restore
    dotnet test ProjetoIntegradorAgenda.slnx --configuration Release --no-build --no-restore

Baseline: 34 testes aprovados. Após a Etapa B: 54 testes aprovados.
Após criar a migration autorizada: 59 testes aprovados.
Incluem associações múltiplas, duplicidade na entrada, reenvio/reativação,
preço/duração inválidos, elegibilidade, preservação de snapshots e NULLs legados,
metadados SQL Server e recuperação após falhas de criação/edição.
Os testes de renderização existentes continuam aprovados.
Após a Etapa C: 93 testes aprovados, incluindo HTTP/model binding/antiforgery.

Os testes de persistência usam EF InMemory. Unicidade, FKs, transações reais e
corridas concorrentes precisam ser validadas em SQL Server isolado após autorização.
Os testes de metadados, operações de migration e geração de SQL não acessam o banco.
Validam as adições esperadas, FKs restritas, ausência de backfill, script incremental
sem operações destrutivas e snapshot consistente com o modelo.
Etapa D concluída no código: consultar ../agendamentos-profissionais.md.
A integração não exigiu novas alterações de esquema. A migration gerada continua sem aplicação.


## Atualização da revisão final — Etapa E

A consulta somente de leitura ao SQL Server configurado confirmou as tabelas e
colunas novas, índices, CHECKs/FKs habilitados e os dois registros de migrations
no histórico EF. O assistente não aplicou essas migrations. As notas de aplicação
pendente acima descrevem o estado conhecido na entrega original.
Consulte revisao-final-profissionais-servicos.md (na pasta docs) para os resultados
atuais: 161 testes aprovados e limitações da homologação visual/transacional.