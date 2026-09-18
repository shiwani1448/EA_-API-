using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jarvis5.Data.EaFms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

public static class Program
{
    public static async Task Main()
    {
        var cfg = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();
        var opts = new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseNpgsql(cfg.GetConnectionString("DefaultConnection"))
            .Options;
        await using var db = new EaFmsDbContext(opts);

        Console.WriteLine("=== SECTION 1: MIGRATION HISTORY ===");
        var allMigrations = await db.Database
            .SqlQueryRaw<string>(
                "SELECT \"MigrationId\" AS \"Value\" FROM public.\"__EFMigrationsHistory\" ORDER BY \"MigrationId\"")
            .ToListAsync();
        foreach (var m in allMigrations)
            Console.WriteLine("APPLIED: " + m);

        var travelNew = allMigrations.Any(m => m == "20260916093641_AddEaTravelFoundation");
        var travelOld = allMigrations.Any(m => m == "20260916064231_AddEaTravelFoundation");
        Console.WriteLine("NEW_MIGRATION_APPLIED=" + travelNew);
        Console.WriteLine("OLD_MIGRATION_APPLIED=" + travelOld);

        Console.WriteLine();
        Console.WriteLine("=== SECTION 2: TABLE AND SEQUENCE EXISTENCE ===");
        var tables = await db.Database
            .SqlQueryRaw<string>(
                "SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema='public' AND table_name IN ('ea_travel_requests','ea_travel_request_cycles') ORDER BY table_name")
            .ToListAsync();
        foreach (var t in tables) Console.WriteLine("TABLE_EXISTS: " + t);

        var seqExists = await db.Database
            .SqlQueryRaw<int>(
                "SELECT COUNT(*)::int AS \"Value\" FROM pg_sequences WHERE schemaname='public' AND sequencename='ea_travel_no_seq'")
            .ToListAsync();
        Console.WriteLine("SEQUENCE_ea_travel_no_seq_EXISTS=" + (seqExists[0] > 0));

        Console.WriteLine();
        Console.WriteLine("=== SECTION 3: ea_travel_requests COLUMNS ===");
        var reqCols = await db.Database
            .SqlQueryRaw<string>(
                "SELECT c.column_name || '|' || c.udt_name || '(' || COALESCE(c.character_maximum_length::text,'') || COALESCE(c.numeric_precision::text,'') || COALESCE(c.datetime_precision::text,'') || ')|' || c.is_nullable || '|default=' || COALESCE(c.column_default,'') AS \"Value\" " +
                "FROM information_schema.columns c " +
                "WHERE c.table_schema='public' AND c.table_name='ea_travel_requests' " +
                "ORDER BY c.ordinal_position")
            .ToListAsync();
        foreach (var col in reqCols) Console.WriteLine("REQ_COL: " + col);

        Console.WriteLine();
        Console.WriteLine("=== SECTION 4: ea_travel_request_cycles COLUMNS ===");
        var cycCols = await db.Database
            .SqlQueryRaw<string>(
                "SELECT c.column_name || '|' || c.udt_name || '(' || COALESCE(c.character_maximum_length::text,'') || COALESCE(c.numeric_precision::text,'') || COALESCE(c.datetime_precision::text,'') || ')|' || c.is_nullable || '|default=' || COALESCE(c.column_default,'') AS \"Value\" " +
                "FROM information_schema.columns c " +
                "WHERE c.table_schema='public' AND c.table_name='ea_travel_request_cycles' " +
                "ORDER BY c.ordinal_position")
            .ToListAsync();
        foreach (var col in cycCols) Console.WriteLine("CYC_COL: " + col);

        Console.WriteLine();
        Console.WriteLine("=== SECTION 5: FOREIGN KEYS ===");
        var fks = await db.Database
            .SqlQueryRaw<string>(
                "SELECT tc.constraint_name || '|' || kcu.table_name || '.' || kcu.column_name || ' -> ' || ccu.table_name || '.' || ccu.column_name || '|DeleteRule=' || rc.delete_rule AS \"Value\" " +
                "FROM information_schema.table_constraints tc " +
                "JOIN information_schema.key_column_usage kcu ON tc.constraint_name=kcu.constraint_name AND tc.table_schema=kcu.table_schema " +
                "JOIN information_schema.referential_constraints rc ON tc.constraint_name=rc.constraint_name AND tc.table_schema=rc.constraint_schema " +
                "JOIN information_schema.constraint_column_usage ccu ON rc.unique_constraint_name=ccu.constraint_name AND rc.unique_constraint_schema=ccu.constraint_schema " +
                "WHERE tc.constraint_type='FOREIGN KEY' AND tc.table_schema='public' AND tc.table_name IN ('ea_travel_requests','ea_travel_request_cycles') " +
                "ORDER BY tc.constraint_name")
            .ToListAsync();
        foreach (var fk in fks) Console.WriteLine("FK: " + fk);

        Console.WriteLine();
        Console.WriteLine("=== SECTION 6: UNIQUE CONSTRAINTS/INDEXES ===");
        var uqs = await db.Database
            .SqlQueryRaw<string>(
                "SELECT i.relname || '|' || t.relname || '|cols=' || string_agg(a.attname, ',' ORDER BY ix.indoption[array_position(ix.indkey::int[], a.attnum::int)]) || '|unique=' || ix.indisunique AS \"Value\" " +
                "FROM pg_index ix JOIN pg_class t ON t.oid=ix.indrelid JOIN pg_class i ON i.oid=ix.indexrelid JOIN pg_attribute a ON a.attrelid=t.oid AND a.attnum=ANY(ix.indkey) JOIN pg_namespace n ON n.oid=t.relnamespace " +
                "WHERE n.nspname='public' AND t.relname IN ('ea_travel_requests','ea_travel_request_cycles') AND ix.indisunique=true " +
                "GROUP BY i.relname, t.relname, ix.indisunique ORDER BY t.relname, i.relname")
            .ToListAsync();
        foreach (var u in uqs) Console.WriteLine("UNIQUE: " + u);

        Console.WriteLine();
        Console.WriteLine("=== SECTION 7: CHECK CONSTRAINTS ===");
        var cks = await db.Database
            .SqlQueryRaw<string>(
                "SELECT cc.constraint_name || '|' || cc.check_clause AS \"Value\" " +
                "FROM information_schema.check_constraints cc " +
                "JOIN information_schema.table_constraints tc ON cc.constraint_name=tc.constraint_name AND cc.constraint_schema=tc.constraint_schema " +
                "WHERE tc.table_schema='public' AND tc.table_name IN ('ea_travel_requests','ea_travel_request_cycles') " +
                "ORDER BY tc.table_name, cc.constraint_name")
            .ToListAsync();
        foreach (var ck in cks) Console.WriteLine("CHECK: " + ck);

        Console.WriteLine();
        Console.WriteLine("=== SECTION 8: ALL INDEXES ===");
        var idxs = await db.Database
            .SqlQueryRaw<string>(
                "SELECT i.relname || '|table=' || t.relname || '|cols=' || string_agg(a.attname, ',' ORDER BY ix.indoption[array_position(ix.indkey::int[], a.attnum::int)]) || '|unique=' || ix.indisunique AS \"Value\" " +
                "FROM pg_index ix JOIN pg_class t ON t.oid=ix.indrelid JOIN pg_class i ON i.oid=ix.indexrelid JOIN pg_attribute a ON a.attrelid=t.oid AND a.attnum=ANY(ix.indkey) JOIN pg_namespace n ON n.oid=t.relnamespace " +
                "WHERE n.nspname='public' AND t.relname IN ('ea_travel_requests','ea_travel_request_cycles') AND NOT ix.indisprimary " +
                "GROUP BY i.relname, t.relname, ix.indisunique ORDER BY t.relname, i.relname")
            .ToListAsync();
        foreach (var idx in idxs) Console.WriteLine("IDX: " + idx);

        Console.WriteLine();
        Console.WriteLine("=== SECTION 9: MONETARY COLUMN TYPES ===");
        var money = await db.Database
            .SqlQueryRaw<string>(
                "SELECT column_name || '|numeric_precision=' || COALESCE(numeric_precision::text,'NULL') || '|numeric_scale=' || COALESCE(numeric_scale::text,'NULL') || '|udt_name=' || udt_name AS \"Value\" " +
                "FROM information_schema.columns " +
                "WHERE table_schema='public' AND table_name='ea_travel_requests' AND column_name IN ('EstimatedTravelCost','EstimatedHotelCost','EstimatedLocalTransportCost','EstimatedHospitalityCost') " +
                "ORDER BY column_name")
            .ToListAsync();
        foreach (var m in money) Console.WriteLine("MONEY: " + m);

        Console.WriteLine();
        Console.WriteLine("=== SECTION 10: SEQUENCE METADATA ===");
        var seqMeta = await db.Database
            .SqlQueryRaw<string>(
                "SELECT 'data_type=' || data_type || '|start=' || start_value || '|increment=' || increment_by || '|min=' || minimum_value || '|max=' || maximum_value || '|cache=' || cache_size AS \"Value\" " +
                "FROM pg_sequences WHERE schemaname='public' AND sequencename='ea_travel_no_seq'")
            .ToListAsync();
        foreach (var s in seqMeta) Console.WriteLine("SEQ: " + s);

        Console.WriteLine();
        Console.WriteLine("=== SECTION 11: COLUMN ABSENCE CHECKS ===");
        var absenceChecks = new[]
        {
            ("ea_travel_requests", "TotalEstimatedCost"),
            ("ea_travel_requests", "SubmittedSnapshot"),
            ("ea_travel_request_cycles", "EaTaskId"),
            ("ea_travel_request_cycles", "ReferenceNo"),
            ("ea_travel_request_cycles", "SubmittedSnapshot"),
        };
        foreach (var (tbl, col) in absenceChecks)
        {
            var cnt = await db.Database
                .SqlQueryRaw<int>(
                    $"SELECT COUNT(*)::int AS \"Value\" FROM information_schema.columns WHERE table_schema='public' AND table_name='{tbl}' AND column_name='{col}'")
                .ToListAsync();
            Console.WriteLine($"ABSENT_CHECK: {tbl}.{col} EXISTS={cnt[0] > 0} (expected=false)");
        }

        // Check for any jsonb columns on travel tables
        var jsonCols = await db.Database
            .SqlQueryRaw<string>(
                "SELECT table_name || '.' || column_name || '|type=' || udt_name AS \"Value\" " +
                "FROM information_schema.columns WHERE table_schema='public' AND table_name IN ('ea_travel_requests','ea_travel_request_cycles') AND udt_name IN ('json','jsonb') " +
                "ORDER BY table_name, column_name")
            .ToListAsync();
        Console.WriteLine("JSON_COLUMNS_COUNT=" + jsonCols.Count + (jsonCols.Count > 0 ? " " + string.Join(",", jsonCols) : " (none — correct)"));

        Console.WriteLine();
        Console.WriteLine("=== SECTION 12: PROTECTED TABLE SCHEMA UNTOUCHED ===");
        var protectedTables = new[] {
            "ea_tasks", "ea_tat_rules", "ea_business_modules", "ea_workflow_instances",
            "ea_workflow_history", "ea_approval_requests", "ea_approval_cycles",
            "ea_meetings", "ea_attachments", "ea_audit_logs", "ea_notifications",
            "ea_followups", "ea_escalations", "ea_escalation_levels"
        };
        foreach (var pt in protectedTables)
        {
            var cnt = await db.Database
                .SqlQueryRaw<int>(
                    $"SELECT COUNT(*)::int AS \"Value\" FROM information_schema.tables WHERE table_schema='public' AND table_name='{pt}'")
                .ToListAsync();
            Console.WriteLine($"PROTECTED: {pt} EXISTS={cnt[0] > 0}");
        }

        Console.WriteLine();
        Console.WriteLine("=== SECTION 13: BUSINESS MODULE CHECK ===");
        var mods = await db.BusinessModules.AsNoTracking()
            .Where(m => EF.Functions.ILike(m.Name, "%travel%") || EF.Functions.ILike(m.Name, "%hospitality%"))
            .Select(m => new { m.Id, m.Name, m.IsActive, m.IsDeleted })
            .ToListAsync();
        if (mods.Count == 0)
            Console.WriteLine("TRAVEL_BM=ABSENT: TRAVEL BUSINESS MODULE CONFIGURATION REQUIRED BEFORE RUNTIME TRAVEL CREATION");
        else
            foreach (var m in mods)
                Console.WriteLine($"TRAVEL_BM: Id={m.Id};Name={m.Name};Active={m.IsActive};Deleted={m.IsDeleted}");

        Console.WriteLine();
        Console.WriteLine("=== SECTION 14: SEQUENCE LAST VALUE (NO CONSUMPTION) ===");
        var seqLast = await db.Database
            .SqlQueryRaw<string>(
                "SELECT COALESCE(last_value::text,'not-yet-called') AS \"Value\" FROM public.ea_travel_no_seq")
            .ToListAsync();
        Console.WriteLine("SEQ_LAST_VALUE=" + seqLast[0]);

        Console.WriteLine();
        Console.WriteLine("=== DONE ===");
    }
}
