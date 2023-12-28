﻿using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Extensions.Logging.EF.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class LogMultiTenant : Migration
    {
        private string _schema = "App";

        public LogMultiTenant()
        {
        }

        public LogMultiTenant(ISchemaDbContext schema)
        {
            _schema = schema.Schema;
        }
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!string.IsNullOrEmpty(_schema))
            {
                migrationBuilder.EnsureSchema(name: _schema);
            }

            migrationBuilder.AlterColumn<Guid>(
                name: "ServiceId",
                schema: _schema,
                table: "Logs",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: _schema,
                table: "Logs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: _schema,
                table: "Logs");

            migrationBuilder.AlterColumn<Guid>(
                name: "ServiceId",
                schema: _schema,
                table: "Logs",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}