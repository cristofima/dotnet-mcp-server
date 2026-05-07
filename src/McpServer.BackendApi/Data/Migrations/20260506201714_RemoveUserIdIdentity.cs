using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace McpServer.BackendApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUserIdIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL Server cannot remove IDENTITY via ALTER COLUMN — drop and recreate the table.
            migrationBuilder.Sql(@"
                CREATE TABLE [Users_New] (
                    [Id] int NOT NULL,
                    [Username] nvarchar(50) NOT NULL,
                    [Role] nvarchar(50) NOT NULL,
                    [LastLogin] datetimeoffset NOT NULL,
                    CONSTRAINT [PK_Users_New] PRIMARY KEY ([Id])
                );

                INSERT INTO [Users_New] ([Id], [Username], [Role], [LastLogin])
                SELECT [Id], [Username], [Role], [LastLogin] FROM [Users];

                DROP TABLE [Users];

                EXEC sp_rename N'Users_New', N'Users';
                EXEC sp_rename N'PK_Users_New', N'PK_Users', N'OBJECT';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Users",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1");
        }
    }
}
