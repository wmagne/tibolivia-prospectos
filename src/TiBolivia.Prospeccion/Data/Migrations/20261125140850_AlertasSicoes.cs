using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TiBolivia.Prospeccion.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlertasSicoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SicoesLecturas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Inicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Fin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Fuente = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Leidos = table.Column<int>(type: "integer", nullable: false),
                    Nuevos = table.Column<int>(type: "integer", nullable: false),
                    Relevantes = table.Column<int>(type: "integer", nullable: false),
                    Exito = table.Column<bool>(type: "boolean", nullable: false),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SicoesLecturas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SicoesPerfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PalabrasClave = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PalabrasExcluidas = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Departamentos = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IncluyeNacional = table.Column<bool>(type: "boolean", nullable: false),
                    MontoMinimo = table.Column<decimal>(type: "numeric(16,2)", precision: 16, scale: 2, nullable: false),
                    Plan = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TarifaMensualBs = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    PilotoHasta = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CorreoAvisos = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    WhatsAppAvisos = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SicoesPerfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SicoesProcesos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Cuce = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Entidad = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Objeto = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Modalidad = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Monto = table.Column<decimal>(type: "numeric(16,2)", precision: 16, scale: 2, nullable: true),
                    Departamento = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    FechaPublicacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FechaLimite = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Enlace = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Estado = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Origen = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    LeidoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SicoesProcesos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SicoesOportunidades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProcesoId = table.Column<int>(type: "integer", nullable: false),
                    PerfilId = table.Column<int>(type: "integer", nullable: false),
                    Puntaje = table.Column<int>(type: "integer", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Notas = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SicoesOportunidades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SicoesOportunidades_SicoesPerfiles_PerfilId",
                        column: x => x.PerfilId,
                        principalTable: "SicoesPerfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SicoesOportunidades_SicoesProcesos_ProcesoId",
                        column: x => x.ProcesoId,
                        principalTable: "SicoesProcesos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SicoesAvisos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OportunidadId = table.Column<int>(type: "integer", nullable: true),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Canal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Destino = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Clave = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Exito = table.Column<bool>(type: "boolean", nullable: false),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EnviadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SicoesAvisos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SicoesAvisos_SicoesOportunidades_OportunidadId",
                        column: x => x.OportunidadId,
                        principalTable: "SicoesOportunidades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SicoesAvisos_OportunidadId_Tipo_Canal",
                table: "SicoesAvisos",
                columns: new[] { "OportunidadId", "Tipo", "Canal" });

            migrationBuilder.CreateIndex(
                name: "IX_SicoesLecturas_Inicio",
                table: "SicoesLecturas",
                column: "Inicio");

            migrationBuilder.CreateIndex(
                name: "IX_SicoesOportunidades_Estado",
                table: "SicoesOportunidades",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_SicoesOportunidades_PerfilId",
                table: "SicoesOportunidades",
                column: "PerfilId");

            migrationBuilder.CreateIndex(
                name: "IX_SicoesOportunidades_ProcesoId_PerfilId",
                table: "SicoesOportunidades",
                columns: new[] { "ProcesoId", "PerfilId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SicoesProcesos_Cuce",
                table: "SicoesProcesos",
                column: "Cuce",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SicoesProcesos_FechaLimite",
                table: "SicoesProcesos",
                column: "FechaLimite");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SicoesAvisos");

            migrationBuilder.DropTable(
                name: "SicoesLecturas");

            migrationBuilder.DropTable(
                name: "SicoesOportunidades");

            migrationBuilder.DropTable(
                name: "SicoesPerfiles");

            migrationBuilder.DropTable(
                name: "SicoesProcesos");
        }
    }
}
