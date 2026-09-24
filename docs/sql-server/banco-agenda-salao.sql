CREATE DATABASE GestaoAgendaSalao;
GO
USE GestaoAgendaSalao;
GO

CREATE TABLE Cliente (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nome VARCHAR(120) NOT NULL,
    Telefone VARCHAR(20) NOT NULL,
    Email VARCHAR(120) NULL
);
GO

CREATE TABLE Servico (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nome VARCHAR(120) NOT NULL,
    Descricao VARCHAR(500) NULL,
    Duracao INT NOT NULL,
    Valor DECIMAL(10,2) NOT NULL
);
GO

CREATE TABLE Agendamento (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ClienteId INT NOT NULL,
    ServicoId INT NOT NULL,
    Data DATE NOT NULL,
    Hora TIME NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'Agendado',
    Observacao VARCHAR(500) NULL,
    CONSTRAINT FK_Agendamento_Cliente FOREIGN KEY (ClienteId) REFERENCES Cliente(Id),
    CONSTRAINT FK_Agendamento_Servico FOREIGN KEY (ServicoId) REFERENCES Servico(Id)
);
GO

CREATE UNIQUE INDEX UX_Agendamento_Data_Hora_Ativo
ON Agendamento(Data, Hora)
WHERE Status <> 'Cancelado';
GO
