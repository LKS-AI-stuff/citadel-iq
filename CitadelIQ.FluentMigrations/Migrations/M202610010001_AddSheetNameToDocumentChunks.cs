using FluentMigrator;

namespace CitadelIQ.FluentMigrations.Migrations;

[Migration(202610010001, "Add SheetName (XLSX worksheet) to DocumentChunks")]
public class M202610010001_AddSheetNameToDocumentChunks : Migration
{
    public override void Up()
    {
        Alter.Table("DocumentChunks")
            .AddColumn("SheetName").AsString(255).Nullable();

        // A chunk comes from a PDF page or an XLSX sheet, never both.
        Execute.Sql("""ALTER TABLE "DocumentChunks" ADD CONSTRAINT "CK_DocumentChunks_PageOrSheet" CHECK ("PageNumber" IS NULL OR "SheetName" IS NULL);""");
    }

    public override void Down()
    {
        Execute.Sql("""ALTER TABLE "DocumentChunks" DROP CONSTRAINT "CK_DocumentChunks_PageOrSheet";""");
        Delete.Column("SheetName").FromTable("DocumentChunks");
    }
}
