IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029235831_AdicionarProfissionais'
)
BEGIN
    CREATE TABLE [Profissional] (
        [Id] int NOT NULL IDENTITY,
        [Nome] varchar(120) NOT NULL,
        [Telefone] varchar(20) NOT NULL,
        [Email] varchar(120) NULL,
        [Especialidade] varchar(120) NOT NULL,
        [Ativo] bit NOT NULL,
        CONSTRAINT [PK_Profissional] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029235831_AdicionarProfissionais'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261029235831_AdicionarProfissionais', N'10.0.14');
END;

COMMIT;
GO

