using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Extensions.Logging.EF.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        private readonly string _schema = "App";

        public Initial()
        {
        }

        public Initial(ISchemaDbContext schema)
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

            migrationBuilder.CreateTable(
                name: "Logs",
                schema: _schema,
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Category = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Message = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Exception = table.Column<string>(type: "text", nullable: true),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TraceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Operation = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Logs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Logs_Level_Timestamp_Operation",
                table: "Logs",
                columns: new[] { "Level", "Timestamp", "Operation" });

            migrationBuilder.CreateIndex(
                name: "IX_Logs_ServiceId",
                table: "Logs",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_TraceId",
                table: "Logs",
                column: "TraceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                schema: _schema,
                name: "Logs");
        }
    }
}
