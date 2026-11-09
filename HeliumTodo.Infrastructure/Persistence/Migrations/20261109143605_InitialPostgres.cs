// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HeliumTodo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Schedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Start = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Block = table.Column<int>(type: "integer", nullable: false),
                    RecurrenceRule = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    LinkedTodoId = table.Column<int>(type: "integer", nullable: true),
                    ReminderMinutes = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudyRoomMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudyRoomId = table.Column<int>(type: "integer", nullable: false),
                    SenderName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsSystemMessage = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyRoomMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudyRooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HostName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsLocalOnly = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultFocusDuration = table.Column<TimeSpan>(type: "interval", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyRooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Todos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DueAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EstimatedDuration = table.Column<TimeSpan>(type: "interval", nullable: false),
                    PlannedStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    FocusedDuration = table.Column<TimeSpan>(type: "interval", nullable: false),
                    ReminderMinutes = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Todos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudyRoomMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudyRoomId = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PeerId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsOnline = table.Column<bool>(type: "boolean", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FocusedToday = table.Column<TimeSpan>(type: "interval", nullable: false),
                    IsFocusing = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyRoomMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyRoomMembers_StudyRooms_StudyRoomId",
                        column: x => x.StudyRoomId,
                        principalTable: "StudyRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FocusSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TodoId = table.Column<int>(type: "integer", nullable: true),
                    PlannedStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PlannedDuration = table.Column<TimeSpan>(type: "interval", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InterruptionCount = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsStudyRoomSession = table.Column<bool>(type: "boolean", nullable: false),
                    StudyRoomCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FocusSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FocusSessions_Todos_TodoId",
                        column: x => x.TodoId,
                        principalTable: "Todos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FocusSessions_PlannedStart",
                table: "FocusSessions",
                column: "PlannedStart");

            migrationBuilder.CreateIndex(
                name: "IX_FocusSessions_StartedAt",
                table: "FocusSessions",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FocusSessions_Status",
                table: "FocusSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FocusSessions_TodoId",
                table: "FocusSessions",
                column: "TodoId");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_Start",
                table: "Schedules",
                column: "Start");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_Status",
                table: "Schedules",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_StudyRoomMembers_StudyRoomId",
                table: "StudyRoomMembers",
                column: "StudyRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyRoomMessages_StudyRoomId_SentAt",
                table: "StudyRoomMessages",
                columns: new[] { "StudyRoomId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudyRooms_Code",
                table: "StudyRooms",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Todos_DueAt",
                table: "Todos",
                column: "DueAt");

            migrationBuilder.CreateIndex(
                name: "IX_Todos_IsCompleted_Priority",
                table: "Todos",
                columns: new[] { "IsCompleted", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_Todos_PlannedStart",
                table: "Todos",
                column: "PlannedStart");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FocusSessions");

            migrationBuilder.DropTable(
                name: "Schedules");

            migrationBuilder.DropTable(
                name: "StudyRoomMembers");

            migrationBuilder.DropTable(
                name: "StudyRoomMessages");

            migrationBuilder.DropTable(
                name: "Todos");

            migrationBuilder.DropTable(
                name: "StudyRooms");
        }
    }
}
