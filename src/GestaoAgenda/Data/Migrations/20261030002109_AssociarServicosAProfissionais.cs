using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestaoAgenda.Data.Migrations
{
    /// <inheritdoc />
    public partial class AssociarServicosAProfissionais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DuracaoMinutosAplicada",
                table: "Agendamento",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecoAplicado",
                table: "Agendamento",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProfissionalServicoId",
                table: "Agendamento",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProfissionalServico",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfissionalId = table.Column<int>(type: "int", nullable: false),
                    ServicoId = table.Column<int>(type: "int", nullable: false),
                    Preco = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DuracaoMinutos = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfissionalServico", x => x.Id);
                    table.CheckConstraint("CK_ProfissionalServico_Duracao", "[DuracaoMinutos] BETWEEN 1 AND 1440");
                    table.CheckConstraint("CK_ProfissionalServico_Preco", "[Preco] > 0");
                    table.ForeignKey(
                        name: "FK_ProfissionalServico_Profissional_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalTable: "Profissional",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProfissionalServico_Servico_ServicoId",
                        column: x => x.ServicoId,
                        principalTable: "Servico",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Agendamento_ProfissionalServicoId",
                table: "Agendamento",
                column: "ProfissionalServicoId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Agendamento_DuracaoAplicada",
                table: "Agendamento",
                sql: "[DuracaoMinutosAplicada] IS NULL OR [DuracaoMinutosAplicada] BETWEEN 1 AND 1440");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Agendamento_PrecoAplicado",
                table: "Agendamento",
                sql: "[PrecoAplicado] IS NULL OR [PrecoAplicado] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProfissionalServico_ProfissionalId_ServicoId",
                table: "ProfissionalServico",
                columns: new[] { "ProfissionalId", "ServicoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfissionalServico_ServicoId",
                table: "ProfissionalServico",
                column: "ServicoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Agendamento_ProfissionalServico_ProfissionalServicoId",
                table: "Agendamento",
                column: "ProfissionalServicoId",
                principalTable: "ProfissionalServico",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Agendamento_ProfissionalServico_ProfissionalServicoId",
                table: "Agendamento");

            migrationBuilder.DropTable(
                name: "ProfissionalServico");

            migrationBuilder.DropIndex(
                name: "IX_Agendamento_ProfissionalServicoId",
                table: "Agendamento");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Agendamento_DuracaoAplicada",
                table: "Agendamento");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Agendamento_PrecoAplicado",
                table: "Agendamento");

            migrationBuilder.DropColumn(
                name: "DuracaoMinutosAplicada",
                table: "Agendamento");

            migrationBuilder.DropColumn(
                name: "PrecoAplicado",
                table: "Agendamento");

            migrationBuilder.DropColumn(
                name: "ProfissionalServicoId",
                table: "Agendamento");
        }
    }
}
