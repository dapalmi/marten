using System.IO;
using System.Linq;
using Baseline;
using Marten.Schema;
using Marten.Util;

namespace Marten.Storage
{
    public class DocumentTable : Table
    {
        public DocumentTable(DocumentMapping mapping) : base(mapping.Table)
        {
            var pgIdType = TypeMappings.GetPgType(mapping.IdMember.GetMemberType());
            AddPrimaryKey(new TableColumn("id", pgIdType));

            AddColumn("data", "jsonb", "NOT NULL");

            AddColumn<LastModifiedColumn>();
            AddColumn<VersionColumn>();
            AddColumn<DotNetTypeColumn>();

            foreach (var field in mapping.DuplicatedFields)
            {
                AddColumn(new DuplicatedFieldColumn(field));
            }

            if (mapping.IsHierarchy())
            {
                AddColumn(new DocumentTypeColumn(mapping));
            }

            if (mapping.DeleteStyle == DeleteStyle.SoftDelete)
            {
                AddColumn<DeletedColumn>();
                AddColumn<DeletedAtColumn>();
            }

            Indexes.AddRange(mapping.Indexes);
            ForeignKeys.AddRange(mapping.ForeignKeys);
        }


    }

    public abstract class SystemColumn : TableColumn
    {
        protected SystemColumn(string name, string type) : base(name, type)
        {
        }
    }

    public class DeletedColumn : SystemColumn
    {
        public DeletedColumn() : base(DocumentMapping.DeletedColumn, "boolean")
        {
            Directive = "DEFAULT FALSE";
            CanAdd = true;
        }
    }

    public class DeletedAtColumn : SystemColumn
    {
        public DeletedAtColumn() : base(DocumentMapping.DeletedAtColumn, "timestamp with time zone")
        {
            CanAdd = true;
            Directive = "NULL";
        }
    }

    public class DocumentTypeColumn : SystemColumn
    {
        public DocumentTypeColumn(DocumentMapping mapping) : base(DocumentMapping.DocumentTypeColumn, "varchar")
        {
            CanAdd = true;
            Directive = $"DEFAULT '{mapping.AliasFor(mapping.DocumentType)}'";
        }
    }

    public class LastModifiedColumn : SystemColumn
    {
        public LastModifiedColumn() : base(DocumentMapping.LastModifiedColumn, "timestamp with time zone")
        {
            Directive = "DEFAULT transaction_timestamp()";
            CanAdd = true;
        }
    }

    public class VersionColumn : SystemColumn
    {
        public VersionColumn() : base(DocumentMapping.VersionColumn, "uuid")
        {
            Directive = "NOT NULL default(md5(random()::text || clock_timestamp()::text)::uuid)";
            CanAdd = true;
        }
    }

    public class DotNetTypeColumn : SystemColumn
    {
        public DotNetTypeColumn() : base(DocumentMapping.DotNetTypeColumn, "varchar")
        {
            CanAdd = true;
        }
    }

    public class DuplicatedFieldColumn : TableColumn
    {
        private readonly DuplicatedField _field;

        public DuplicatedFieldColumn(DuplicatedField field) : base(field.ColumnName, field.PgType)
        {
            CanAdd = true;
            _field = field;
        }

        public override string AddColumnSql(Table table)
        {
            return $"{base.AddColumnSql(table)};update {table.Identifier} set {_field.UpdateSqlFragment()};";
        }
    }
}