using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TiBolivia.Prospeccion.Data.Migrations
{
    /// <inheritdoc />
    public partial class VentasTareasYAdjuntos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Prospectos_AsignadoAId",
                table: "Prospectos");

            migrationBuilder.AddColumn<bool>(
                name: "DatosPorVerificar",
                table: "Prospectos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Direccion",
                table: "Prospectos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaProximaAccion",
                table: "Prospectos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fuente",
                table: "Prospectos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Necesidad",
                table: "Prospectos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Prioridad",
                table: "Prospectos",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProximaAccion",
                table: "Prospectos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorEstimado",
                table: "Prospectos",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "AdjuntoContenido",
                table: "Plantillas",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdjuntoNombre",
                table: "Plantillas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prospectos_AsignadoAId_FechaProximaAccion",
                table: "Prospectos",
                columns: new[] { "AsignadoAId", "FechaProximaAccion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Prospectos_AsignadoAId_FechaProximaAccion",
                table: "Prospectos");

            migrationBuilder.DropColumn(
                name: "DatosPorVerificar",
                table: "Prospectos");

            migrationBuilder.DropColumn(
                name: "Direccion",
                table: "Prospectos");

            migrationBuilder.DropColumn(
                name: "FechaProximaAccion",
                table: "Prospectos");

            migrationBuilder.DropColumn(
                name: "Fuente",
                table: "Prospectos");

            migrationBuilder.DropColumn(
                name: "Necesidad",
                table: "Prospectos");

            migrationBuilder.DropColumn(
                name: "Prioridad",
                table: "Prospectos");

            migrationBuilder.DropColumn(
                name: "ProximaAccion",
                table: "Prospectos");

            migrationBuilder.DropColumn(
                name: "ValorEstimado",
                table: "Prospectos");

            migrationBuilder.DropColumn(
                name: "AdjuntoContenido",
                table: "Plantillas");

            migrationBuilder.DropColumn(
                name: "AdjuntoNombre",
                table: "Plantillas");

            migrationBuilder.CreateIndex(
                name: "IX_Prospectos_AsignadoAId",
                table: "Prospectos",
                column: "AsignadoAId");
        }
    }
}
