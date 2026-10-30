using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestaoAgenda.Data.Migrations
{
    /// <summary>
    /// Evolução de banco existente, criado pelo script SQL original.
    /// O snapshot registra o modelo legado sem recriar suas tabelas.
    /// </summary>
    public partial class AdicionarProfissionais : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Profissional",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    Telefone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: true),
                    Especialidade = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profissional", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Profissional");
        }
    }
}