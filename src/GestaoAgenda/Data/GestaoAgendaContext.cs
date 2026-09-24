using GestaoAgenda.Models;
using Microsoft.EntityFrameworkCore;

namespace GestaoAgenda.Data;

public class GestaoAgendaContext(DbContextOptions<GestaoAgendaContext> options) : DbContext(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Servico> Servicos => Set<Servico>();
    public DbSet<Agendamento> Agendamentos => Set<Agendamento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("SQL_Latin1_General_CP1_CI_AI");

        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.ToTable("Cliente");
            entity.Property(e => e.Nome).HasMaxLength(120).IsUnicode(false).IsRequired();
            entity.Property(e => e.Telefone).HasMaxLength(20).IsUnicode(false).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(120).IsUnicode(false);
        });

        modelBuilder.Entity<Servico>(entity =>
        {
            entity.ToTable("Servico");
            entity.Property(e => e.Nome).HasMaxLength(120).IsUnicode(false).IsRequired();
            entity.Property(e => e.Descricao).HasMaxLength(500).IsUnicode(false);
            entity.Property(e => e.Valor).HasColumnType("decimal(10,2)");
        });

        modelBuilder.Entity<Agendamento>(entity =>
        {
            entity.ToTable("Agendamento");
            entity.Property(e => e.Status).HasMaxLength(20).IsUnicode(false).IsRequired();
            entity.Property(e => e.Observacao).HasMaxLength(500).IsUnicode(false);
            entity.Property(e => e.Data).HasColumnType("date");
            entity.Property(e => e.Hora).HasColumnType("time");

            entity.HasIndex(e => new { e.Data, e.Hora })
                .HasFilter("[Status] <> 'Cancelado'")
                .IsUnique();

            entity.HasOne(e => e.Cliente)
                .WithMany(c => c.Agendamentos)
                .HasForeignKey(e => e.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Servico)
                .WithMany(s => s.Agendamentos)
                .HasForeignKey(e => e.ServicoId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
