using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayrollApi.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeePan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Pan",
                table: "Employees",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Pan",
                table: "Employees");
        }
    }
}
