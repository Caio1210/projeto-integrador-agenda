# Cadastro de profissionais — migration criada, aplicação pendente

A migration AdicionarProfissionais foi criada após autorização. Não foi aplicada
e nenhum comando de alteração do banco foi executado.

| Coluna | Tipo SQL Server | Nulo | Regra |
|---|---|---|---|
| Id | int IDENTITY(1,1) | não | chave primária |
| Nome | varchar(120) | não | obrigatório |
| Telefone | varchar(20) | não | obrigatório |
| Email | varchar(120) | sim | formato de e-mail quando informado |
| Especialidade | varchar(120) | não | obrigatório |
| Ativo | bit | não | true por padrão na aplicação; false indica inativo |

## Escopo e baseline

A migration cria apenas Profissional. O script idempotente
adicionar-profissionais.sql também cria __EFMigrationsHistory quando necessário
e registra a migration na tabela de histórico.

Cliente, Servico e Agendamento são considerados preexistentes, conforme o script
banco-agenda-salao.sql. O snapshot completo registra o modelo atual como referência
para futuras diferenças, sem gerar DDL para as tabelas legadas nesta migration.
Esta migration não inicializa uma instalação vazia.

O esquema real não foi comparado ao snapshot nesta etapa. Antes da aplicação,
conferir as tabelas, índices, FKs e collation no banco de destino. O script original
e o modelo têm diferenças conhecidas de nomes de índices/FKs, índices nas FKs e
default SQL de Status; elas não são corrigidas por esta migration. Futuras
migrations que alterem esses objetos precisam considerar os nomes reais.

Nenhuma FK foi adicionada a Agendamento ou Servico. A política global de conflitos
de horários permanece a mesma. Não há exclusão física no cadastro.

## Ferramentas e revisão

A ferramenta local dotnet-ef está fixada em 10.0.14 em dotnet-tools.json.
A GestaoAgendaContextFactory permite executar os comandos de design-time sem
iniciar Program.cs e sem executar a carga demonstrativa. Usa os appsettings
copiados para o diretório de saída e permite sobrescrever a conexão por
ConnectionStrings__DefaultConnection, sem incluir credenciais no código.

Na raiz do repositório:

    dotnet tool restore
    dotnet build ProjetoIntegradorAgenda.slnx -c Release
    dotnet test ProjetoIntegradorAgenda.slnx -c Release
    dotnet ef migrations script --idempotent --project src/GestaoAgenda --configuration Release --output docs/sql-server/adicionar-profissionais.sql

Gerar script não aplica mudanças. Não existe atualização automática na inicialização.
A aplicação no banco deve ocorrer em etapa separada, depois da revisão e confirmação
do destino. Até lá, as telas dependentes de Profissional apresentam mensagem de erro
quando a tabela não existe.

O método Down remove apenas Profissional: a reversão apagaria os registros dessa
tabela e não deve ser executada sem avaliação e autorização específica.