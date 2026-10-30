# Etapa C — interface de serviços oferecidos

## Entrega

O cadastro e a edição de profissionais usam ProfissionalFormViewModel e a seção
Serviços oferecidos. Todos os serviços do catálogo aparecem com sugestões de preço
e duração globais; associações existentes carregam seus valores próprios e status,
inclusive inativos. Os detalhes exibem a tabela com preço, duração, status e links
de edição que levam diretamente à oferta no formulário.

Os formulários enviam a configuração completa. Desmarcar oferta existente a inativa,
sem excluir a associação. O profissional pode ser salvo sem ofertas. Preço/duração
de Servico não são modificados. Salvamento usa ProfissionalServicoService, com a
gravação conjunta implementada na Etapa B.

Nomes dos serviços são sempre obtidos do banco. Campos de saída não são vinculados
no POST. Id de criação é zero; na edição deve corresponder à rota. Listas
duplicadas, serviços desconhecidos e formulários sem todos os itens do catálogo
são rejeitados. Se o catálogo muda, o formulário deve ser recarregado antes de
salvar, para evitar inativação silenciosa de ofertas omitidas.

## Validação e interface

- Bootstrap/grid do projeto; campos empilhados no celular e tabela de detalhes
  com rolagem horizontal.
- Preço textual pt-BR com inputmode decimal. Exemplos aceitos: 40,00; 40,5;
  40; 1.234,56. Sem ponto decimal; milhar exige centavos.
- Servidor converte com cultura explícita, valida o limite decimal(18,2) e
  rejeita valores não positivos ou com mais de duas casas decimais.
- Duração de 1 a 1440 minutos, obrigatória nas ofertas selecionadas.
- JavaScript específico valida preço e duração e desabilita campos de ofertas
  não selecionadas. Sem JavaScript, os mesmos dados são validados no servidor.
- Mensagens de sucesso, erros de campo e de carregamento. Entradas preservadas
  após erro; carregamento incompleto bloqueia salvar e oferece recarregamento.
- Antiforgery preservado. Não há autenticação/segregação por estabelecimento
  no mecanismo atual; nenhuma política de acesso nova foi inventada.

## Reenvios

O botão é bloqueado após validação e o servidor usa EnviosFormularioProfissional.
Tokens são imprevisíveis, vinculados a criar ou ao Id de edição, válidos por
30 minutos e limitados a 2048 entradas na memória da instância. Uma operação
simultânea recebe 409; um reenvio já concluído redireciona sem gravar de novo.
Falha de gravação libera o token para nova tentativa.
Tokens inválidos, expirados ou perdidos após reinício exigem recarregar.

Esta proteção é local à instância. Não oferece idempotência distribuída entre
múltiplas instâncias e não substitui o índice único das associações. Essa limitação
deve ser considerada caso a aplicação seja executada em múltiplos servidores.

## Arquivos

Criados:
- ViewModels/ProfissionalFormViewModel.cs (inclui itens de oferta, conversão pt-BR
  e modelo de detalhes).
- Services/EnviosFormularioProfissional.cs.
- Views/Profissional/_ServicosOferecidos.cshtml.
- wwwroot/js/profissional-servicos.js.
- Testes de formulário e HTTP.

Modificados:
- ProfissionalController: carregar catálogo/ofertas e salvar pelo serviço.
- Views de criação, edição, detalhes e partials de formulário/validação.
- Program: registro singleton do controle de reenvios.
- Testes de controller e renderização adaptados ao ViewModel.
- Projeto de testes: Microsoft.AspNetCore.TestHost 10.0.14, somente para testes.

## Validação e limites

Restore, build Release e testes executados na solução ProjetoIntegradorAgenda.slnx.
Os testes HTTP usam o pipeline MVC real, antiforgery e model binding com TestServer,
um servidor em memória, e banco EF InMemory isolado. Não iniciam Program.cs nem
executam a carga demonstrativa. Cobrem POST inválido, CSRF, reenvios, preços pt-BR,
edição, inativação e campos adulterados. Há testes de renderização Razor com links
reais, estados vazios e falha de carregamento, além dos testes anteriores de
persistência e migrations.

Não houve aplicação de migrations, acesso de escrita ao SQL Server nem mudança
do esquema nesta etapa. O modelo permanece consistente com a migration gerada.
A execução em SQL Server real e a avaliação visual em navegador/celular continuam
pendentes. Não há runtime Node disponível nos locais verificados; a interação
JavaScript não foi executada em navegador nesta etapa.

Etapa D implementada: seleção elegível, snapshots e conflitos por duração. Ver
agendamentos-profissionais.md para regras, testes e limitações. A migration
AssociarServicosAProfissionais continua sem aplicação autorizada.


## Atualização da revisão final — Etapa E

A consulta somente de leitura ao SQL Server configurado confirmou as tabelas e
colunas novas, índices, CHECKs/FKs habilitados e os dois registros de migrations
no histórico EF. O assistente não aplicou essas migrations. As notas de aplicação
pendente acima descrevem o estado conhecido na entrega original.
Consulte revisao-final-profissionais-servicos.md (na pasta docs) para os resultados
atuais: 161 testes aprovados e limitações da homologação visual/transacional.