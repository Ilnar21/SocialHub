using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SocialHub.Community.Infrastructure.Persistence;

#nullable disable

namespace SocialHub.Community.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CommunityDbContext))]
[Migration("20260525031000_AddCommunityUsernames")]
public partial class AddCommunityUsernames : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Username",
            table: "communities",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "NormalizedUsername",
            table: "communities",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "");

        migrationBuilder.Sql(@"
UPDATE communities
SET ""Username"" = lower(concat(
    trim(both '_' from regexp_replace(lower(""Name""), '[^a-z0-9]+', '_', 'g')),
    '_',
    left(replace(""Id""::text, '-', ''), 8)
));

UPDATE communities
SET ""Username"" = concat('community_', left(replace(""Id""::text, '-', ''), 8))
WHERE ""Username"" IS NULL OR ""Username"" = '' OR ""Username"" !~ '^[[:alnum:]]';

UPDATE communities
SET ""NormalizedUsername"" = upper(""Username"");
");

        migrationBuilder.CreateIndex(
            name: "IX_communities_NormalizedUsername",
            table: "communities",
            column: "NormalizedUsername",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_communities_NormalizedUsername",
            table: "communities");

        migrationBuilder.DropColumn(
            name: "NormalizedUsername",
            table: "communities");

        migrationBuilder.DropColumn(
            name: "Username",
            table: "communities");
    }
}
