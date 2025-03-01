﻿using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Imgeneus.Database.Migrations
{
    public partial class AddWarehouseItems : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WarehouseItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    TypeId = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    Slot = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    Count = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    Quality = table.Column<ushort>(type: "smallint unsigned", nullable: false),
                    GemTypeId1 = table.Column<int>(type: "int", nullable: false),
                    GemTypeId2 = table.Column<int>(type: "int", nullable: false),
                    GemTypeId3 = table.Column<int>(type: "int", nullable: false),
                    GemTypeId4 = table.Column<int>(type: "int", nullable: false),
                    GemTypeId5 = table.Column<int>(type: "int", nullable: false),
                    GemTypeId6 = table.Column<int>(type: "int", nullable: false),
                    Craftname = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreationTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpirationTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    HasDyeColor = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DyeColorAlpha = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    DyeColorSaturation = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    DyeColorR = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    DyeColorG = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    DyeColorB = table.Column<byte>(type: "tinyint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarehouseItems_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseItems_UserId",
                table: "WarehouseItems",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WarehouseItems");
        }
    }
}
