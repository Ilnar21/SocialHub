using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SocialHub.Community.Infrastructure.Persistence;

#nullable disable

namespace SocialHub.Community.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CommunityDbContext))]
[Migration("20260526090000_AddCommunityStatus")]
public partial class AddCommunityStatus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Status",
            table: "communities",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "Active");

        migrationBuilder.AddColumn<string>(
            name: "BlockReason",
            table: "communities",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "BlockedAtUtc",
            table: "communities",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "BlockedByUserId",
            table: "communities",
            type: "uuid",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Status",
            table: "communities");

        migrationBuilder.DropColumn(
            name: "BlockReason",
            table: "communities");

        migrationBuilder.DropColumn(
            name: "BlockedAtUtc",
            table: "communities");

        migrationBuilder.DropColumn(
            name: "BlockedByUserId",
            table: "communities");
    }
}
