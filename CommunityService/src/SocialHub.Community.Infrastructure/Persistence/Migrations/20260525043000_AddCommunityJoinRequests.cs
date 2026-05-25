using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SocialHub.Community.Infrastructure.Persistence;

#nullable disable

namespace SocialHub.Community.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CommunityDbContext))]
[Migration("20260525043000_AddCommunityJoinRequests")]
public partial class AddCommunityJoinRequests : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "community_join_requests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ReviewComment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_community_join_requests", x => x.Id);
                table.ForeignKey(
                    name: "FK_community_join_requests_communities_CommunityId",
                    column: x => x.CommunityId,
                    principalTable: "communities",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_community_join_requests_CommunityId_Status",
            table: "community_join_requests",
            columns: new[] { "CommunityId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_community_join_requests_CommunityId_UserId_Status",
            table: "community_join_requests",
            columns: new[] { "CommunityId", "UserId", "Status" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "community_join_requests");
    }
}
