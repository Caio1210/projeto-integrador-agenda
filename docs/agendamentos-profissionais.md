# Etapa D — agendamentos com profissionais

## Fluxo implementado

O formulário usa AgendamentoFormViewModel, sem propriedades de preço ou duração
vinculáveis pelo navegador. O usuário escolhe cliente, serviço e profissional.
A opção de profissional identifica ProfissionalServico, não um Id livre de
profissional. O endpoint GET /agendamento/profissionais?servicoId=... retorna somente
ofertas ativas de profissionais ativos para o serviço solicitado.

O formulário mostra preço, duração e término previstos. Ao mudar o serviço, o
JavaScript cancela a busca anterior, limpa a seleção e carrega as ofertas elegíveis.
Respostas antigas não substituem uma seleção mais recente. A interface usa o
Bootstrap e layout atuais. Sem JavaScript, Carregar profissionais faz POST com
antiforgery para recarregar as opções, sem gravar o agendamento.

Os dados são confirmados novamente pelo AgendamentoService no momento de gravar.
Valores enviados como PrecoAplicado, DuracaoMinutosAplicada ou dados de resumo são
ignorados pelo model binding. As consultas para listagem, detalhes e semana incluem
o profissional por meio da associação, sem consultas por linha.

## Preço e histórico

Novas reservas e trocas explícitas de serviço/oferta capturam preço e duração
vigentes no banco. Ao manter a mesma oferta, os snapshots já reservados são
preservados, inclusive ao reagendar para outro horário com uma oferta elegível.
Uma alteração do preço/duração da oferta ou dos padrões de Servico não reprecifica
as reservas existentes.

Apenas mudanças de observação/status que não reativam uma reserva cancelada podem
manter profissional/oferta inativos e dados históricos desconhecidos.
Mudanças de cliente, serviço, oferta, data ou hora, e reativação de cancelado,
exigem oferta elegível novamente.

A opção histórica/inativa aparece na edição apenas para permitir preservar a
reserva atual; não é retornada como elegível pelo endpoint público. O servidor
recusa seu uso para reagendamento/reativação.

Registros antigos com NULL permanecem com NULL. Não há preenchimento retroativo.
Selecionar explicitamente uma nova oferta para uma reserva antiga passa a ser uma
mudança de reserva com valores atuais, exibidos antes de salvar.

## Intervalos e conflitos

Termino é calculado de Data + Hora + DuracaoMinutosAplicada; é NotMapped e não
introduz coluna ou migration. A duração aceita é de 1 a 1440 minutos.
Horários fora do dia e término além de DateTime.MaxValue são rejeitados.

O conflito permanece GLOBAL para o salão, inclusive entre profissionais diferentes.
Dois intervalos conflitam quando inicioNovo < fimExistente e inicioExistente < fimNovo.
Intervalos adjacentes são permitidos. O próprio Id é excluído na edição.
Cancelados não bloqueiam; concluídos continuam bloqueando, como na regra anterior.

A busca é limitada ao dia anterior e aos dias atingidos pelo novo intervalo,
suficiente para durações conhecidas de no máximo 1440 minutos. Detecta travessias
da meia-noite e não considera o dia seguinte quando o término é exatamente 00:00.

Para agendamento não cancelado sem duração histórica válida, aplica-se a política
conservadora aprovada: bloquear novas reservas/reagendamentos que atinjam seu dia.
Não se presume a duração atual do serviço para reconstruir o passado.
Cancelar o registro permite liberar o dia sem inventar valores históricos.

## Concorrência e falhas

AgendamentoService é o caminho de gravação dos POSTs Create/Edit.
Um semáforo global na instância serializa essas operações. No SQL Server, a leitura
do original, elegibilidade, consulta de conflitos e gravação ocorrem dentro de
transação serializável, protegendo linhas e intervalos contra alterações concorrentes.
A transação é executada pela execution strategy existente do EF, compatível com
EnableRetryOnFailure. As entidades da tentativa são desvinculadas em finally.
Falhas de persistência/retries geram mensagem compreensível e 503, sem detalhes
técnicos no HTML. O índice único existente (Data, Hora) permanece como proteção adicional.

Falhas ao carregar formulário, agenda ou ofertas têm mensagem de erro e opção de
nova tentativa; carregamento incompleto bloqueia salvar. O formulário mantém os
dados enviados após erros de validação/gravação. CSRF permanece obrigatório.
Não foi introduzida autenticação nem segregação por estabelecimento: não existem
esses mecanismos no projeto atual.

## Arquivos principais

Criados:
- Models/IntervaloAgendamento.cs
- ViewModels/AgendamentoFormViewModel.cs
- Services/AgendamentoService.cs
- Views/Agendamento/_AgendamentoValidationScripts.cshtml
- wwwroot/js/agendamento-ofertas.js
- AgendamentoServiceTests e AgendamentoHttpTests no projeto de testes

Modificados:
- Models/Agendamento.cs: término calculado, sem mudança de esquema
- AgendamentoController: endpoint elegível, ViewModel e gravação pelo serviço
- AgendamentoRepository: Include do profissional e consultas sem tracking
- Views de criação, edição, formulário, detalhes, lista e semana
- Program: registro do serviço de agendamento

## Validação

Na raiz:

    dotnet restore ProjetoIntegradorAgenda.slnx
    dotnet build ProjetoIntegradorAgenda.slnx --configuration Release --no-restore
    dotnet test ProjetoIntegradorAgenda.slnx --configuration Release --no-build --no-restore
    dotnet ef migrations has-pending-model-changes --project src/GestaoAgenda --configuration Release --no-build

135 testes aprovados, incluindo os 93 anteriores. Build sem avisos ou erros.
Modelo consistente com a migration já gerada.

Testes novos cobrem ofertas/profissionais inativos, serviço incompatível, preço e
duração aplicados no servidor, sobreposições/adjacências, conflito global, meia-noite,
cancelamento/reativação, reservas legadas, preservação de snapshots, troca explícita,
falha de gravação, carregamento, consultas semanais, renderização Razor, binding,
CSRF e requisições concorrentes no pipeline MVC da mesma instância.

Os testes usam EF InMemory e TestServer, sem executar Program.cs ou sua carga
demonstrativa. Eles não comprovam isolamento/locks e retries em SQL Server real.
Concorrência entre instâncias e comportamento transacional precisam de validação
em SQL Server de teste após autorização da preparação do banco.

Nenhuma migration foi criada ou aplicada nesta etapa; nenhum banco real foi
modificado. A migration AssociarServicosAProfissionais continua pendente de
aplicação autorizada. Não publicar esta versão com o esquema antigo.

A aparência e execução do JavaScript em navegador/celular não foram validadas
visualmente nesta etapa. A renderização e o fallback sem JavaScript foram testados
por HTTP. Sem commit, push ou merge.

## Atualização da revisão final — Etapa E

A consulta somente de leitura ao SQL Server configurado confirmou as tabelas e
colunas novas, índices, CHECKs/FKs habilitados e os dois registros de migrations
no histórico EF. O assistente não aplicou essas migrations. As notas de aplicação
pendente acima descrevem o estado conhecido na entrega original.
Consulte revisao-final-profissionais-servicos.md (na pasta docs) para os resultados
atuais: 161 testes aprovados e limitações da homologação visual/transacional.