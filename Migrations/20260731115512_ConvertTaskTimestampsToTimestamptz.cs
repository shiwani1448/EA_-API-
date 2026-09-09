using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jarvis5.Migrations
{
    /// <inheritdoc />
    public partial class ConvertTaskTimestampsToTimestamptz : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every existing value in these columns is already UTC wall-clock time
            // stored as a naive "timestamp" (see Clock.cs). A plain ALTER COLUMN ...
            // TYPE timestamptz reinterprets naive values using the *session* TimeZone
            // (not UTC), silently shifting them. USING ... AT TIME ZONE 'UTC' tells
            // Postgres to treat the naive value as UTC instead, which is a no-op on
            // the actual instant — only the column's tz-awareness changes.
            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_TaskHistory\" ALTER COLUMN \"ActionDate\" TYPE timestamptz USING \"ActionDate\" AT TIME ZONE 'UTC';");

            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_Task\" ALTER COLUMN \"OverallStartDate\" TYPE timestamptz USING \"OverallStartDate\" AT TIME ZONE 'UTC';");

            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_Task\" ALTER COLUMN \"OverallEndDate\" TYPE timestamptz USING \"OverallEndDate\" AT TIME ZONE 'UTC';");

            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_Task\" ALTER COLUMN \"ModifiedDate\" TYPE timestamptz USING \"ModifiedDate\" AT TIME ZONE 'UTC';");

            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_Task\" ALTER COLUMN \"CreatedDate\" TYPE timestamptz USING \"CreatedDate\" AT TIME ZONE 'UTC';");

            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_StageMaster\" ALTER COLUMN \"ModifiedDate\" TYPE timestamptz USING \"ModifiedDate\" AT TIME ZONE 'UTC';");

            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_StageMaster\" ALTER COLUMN \"CreatedDate\" TYPE timestamptz USING \"CreatedDate\" AT TIME ZONE 'UTC';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Mirror of Up(): "col AT TIME ZONE 'UTC'" on a timestamptz yields the
            // naive UTC wall-clock value directly, regardless of session TimeZone.
            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_TaskHistory\" ALTER COLUMN \"ActionDate\" TYPE timestamp USING \"ActionDate\" AT TIME ZONE 'UTC';");

            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_Task\" ALTER COLUMN \"OverallStartDate\" TYPE timestamp USING \"OverallStartDate\" AT TIME ZONE 'UTC';");

            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_Task\" ALTER COLUMN \"OverallEndDate\" TYPE timestamp USING \"OverallEndDate\" AT TIME ZONE 'UTC';");

            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_Task\" ALTER COLUMN \"ModifiedDate\" TYPE timestamp USING \"ModifiedDate\" AT TIME ZONE 'UTC';");

            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_Task\" ALTER COLUMN \"CreatedDate\" TYPE timestamp USING \"CreatedDate\" AT TIME ZONE 'UTC';");

            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_StageMaster\" ALTER COLUMN \"ModifiedDate\" TYPE timestamp USING \"ModifiedDate\" AT TIME ZONE 'UTC';");

            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_StageMaster\" ALTER COLUMN \"CreatedDate\" TYPE timestamp USING \"CreatedDate\" AT TIME ZONE 'UTC';");
        }
    }
}
