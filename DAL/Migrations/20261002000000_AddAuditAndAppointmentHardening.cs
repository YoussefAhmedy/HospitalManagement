using Hospital.DAL.DataBase;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hospital.DAL.Migrations;

[DbContext(typeof(HospitalDbContext))]
[Migration("20261002000000_AddAuditAndAppointmentHardening")]
public partial class AddAuditAndAppointmentHardening : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion",
            table: "Appointments",
            type: "rowversion",
            rowVersion: true,
            nullable: false);

        migrationBuilder.CreateTable(
            name: "AuditLogs",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                EventType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                SubjectUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                ResourceType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                ResourceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_AuditLogs", entry => entry.Id));

        migrationBuilder.CreateIndex(
            name: "IX_Appointments_DoctorID_AppointmentDate",
            table: "Appointments",
            columns: new[] { "DoctorID", "AppointmentDate" });
        migrationBuilder.CreateIndex(
            name: "IX_Appointments_PatientID_AppointmentDate",
            table: "Appointments",
            columns: new[] { "PatientID", "AppointmentDate" });
        migrationBuilder.CreateIndex(
            name: "IX_MedicalRecords_DoctorID_RecordDate",
            table: "MedicalRecords",
            columns: new[] { "DoctorID", "RecordDate" });
        migrationBuilder.CreateIndex(
            name: "IX_MedicalRecords_PatientID_RecordDate",
            table: "MedicalRecords",
            columns: new[] { "PatientID", "RecordDate" });
        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_ActorUserId_OccurredAtUtc",
            table: "AuditLogs",
            columns: new[] { "ActorUserId", "OccurredAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_OccurredAtUtc",
            table: "AuditLogs",
            column: "OccurredAtUtc");
        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_ResourceType_ResourceId_OccurredAtUtc",
            table: "AuditLogs",
            columns: new[] { "ResourceType", "ResourceId", "OccurredAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AuditLogs");
        migrationBuilder.DropIndex(name: "IX_Appointments_DoctorID_AppointmentDate", table: "Appointments");
        migrationBuilder.DropIndex(name: "IX_Appointments_PatientID_AppointmentDate", table: "Appointments");
        migrationBuilder.DropIndex(name: "IX_MedicalRecords_DoctorID_RecordDate", table: "MedicalRecords");
        migrationBuilder.DropIndex(name: "IX_MedicalRecords_PatientID_RecordDate", table: "MedicalRecords");
        migrationBuilder.DropColumn(name: "RowVersion", table: "Appointments");
    }
}
