using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Extensions.Logging.EF.SqlServer.MetricsMigrations
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
                name: "CategoryLogMetrics",
                schema: _schema,
                columns: table => new
                {
                    Category = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DbgCount = table.Column<long>(type: "bigint", nullable: false),
                    InfCount = table.Column<long>(type: "bigint", nullable: false),
                    ErrCount = table.Column<long>(type: "bigint", nullable: false),
                    WrnCount = table.Column<long>(type: "bigint", nullable: false),
                    CriCount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryLogMetrics", x => new { x.Category, x.Timestamp });
                });

            migrationBuilder.CreateTable(
                name: "OperationLogMetrics",
                schema: _schema,
                columns: table => new
                {
                    Operation = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DbgCount = table.Column<long>(type: "bigint", nullable: false),
                    InfCount = table.Column<long>(type: "bigint", nullable: false),
                    ErrCount = table.Column<long>(type: "bigint", nullable: false),
                    WrnCount = table.Column<long>(type: "bigint", nullable: false),
                    CriCount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationLogMetrics", x => new { x.Operation, x.Timestamp });
                });

            migrationBuilder.CreateTable(
                name: "ServiceLogMetrics",
                schema: _schema,
                columns: table => new
                {
                    ServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DbgCount = table.Column<long>(type: "bigint", nullable: false),
                    InfCount = table.Column<long>(type: "bigint", nullable: false),
                    ErrCount = table.Column<long>(type: "bigint", nullable: false),
                    WrnCount = table.Column<long>(type: "bigint", nullable: false),
                    CriCount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceLogMetrics", x => new { x.ServiceId, x.Timestamp });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategoryLogMetrics",
                schema: _schema);

            migrationBuilder.DropTable(
                name: "OperationLogMetrics",
                schema: _schema);

            migrationBuilder.DropTable(
                name: "ServiceLogMetrics",
                schema: _schema);
        }
    }
}
