BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030002109_AssociarServicosAProfissionais'
)
BEGIN
    ALTER TABLE [Agendamento] ADD [DuracaoMinutosAplicada] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030002109_AssociarServicosAProfissionais'
)
BEGIN
    ALTER TABLE [Agendamento] ADD [PrecoAplicado] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030002109_AssociarServicosAProfissionais'
)
BEGIN
    ALTER TABLE [Agendamento] ADD [ProfissionalServicoId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030002109_AssociarServicosAProfissionais'
)
BEGIN
    CREATE TABLE [ProfissionalServico] (
        [Id] int NOT NULL IDENTITY,
        [ProfissionalId] int NOT NULL,
        [ServicoId] int NOT NULL,
        [Preco] decimal(18,2) NOT NULL,
        [DuracaoMinutos] int NOT NULL,
        [Ativo] bit NOT NULL,
        CONSTRAINT [PK_ProfissionalServico] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ProfissionalServico_Duracao] CHECK ([DuracaoMinutos] BETWEEN 1 AND 1440),
        CONSTRAINT [CK_ProfissionalServico_Preco] CHECK ([Preco] > 0),
        CONSTRAINT [FK_ProfissionalServico_Profissional_ProfissionalId] FOREIGN KEY ([ProfissionalId]) REFERENCES [Profissional] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProfissionalServico_Servico_ServicoId] FOREIGN KEY ([ServicoId]) REFERENCES [Servico] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030002109_AssociarServicosAProfissionais'
)
BEGIN
    CREATE INDEX [IX_Agendamento_ProfissionalServicoId] ON [Agendamento] ([ProfissionalServicoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030002109_AssociarServicosAProfissionais'
)
BEGIN
    EXEC(N'ALTER TABLE [Agendamento] ADD CONSTRAINT [CK_Agendamento_DuracaoAplicada] CHECK ([DuracaoMinutosAplicada] IS NULL OR [DuracaoMinutosAplicada] BETWEEN 1 AND 1440)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030002109_AssociarServicosAProfissionais'
)
BEGIN
    EXEC(N'ALTER TABLE [Agendamento] ADD CONSTRAINT [CK_Agendamento_PrecoAplicado] CHECK ([PrecoAplicado] IS NULL OR [PrecoAplicado] > 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030002109_AssociarServicosAProfissionais'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProfissionalServico_ProfissionalId_ServicoId] ON [ProfissionalServico] ([ProfissionalId], [ServicoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030002109_AssociarServicosAProfissionais'
)
BEGIN
    CREATE INDEX [IX_ProfissionalServico_ServicoId] ON [ProfissionalServico] ([ServicoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030002109_AssociarServicosAProfissionais'
)
BEGIN
    ALTER TABLE [Agendamento] ADD CONSTRAINT [FK_Agendamento_ProfissionalServico_ProfissionalServicoId] FOREIGN KEY ([ProfissionalServicoId]) REFERENCES [ProfissionalServico] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030002109_AssociarServicosAProfissionais'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261030002109_AssociarServicosAProfissionais', N'10.0.14');
END;

COMMIT;
GO

