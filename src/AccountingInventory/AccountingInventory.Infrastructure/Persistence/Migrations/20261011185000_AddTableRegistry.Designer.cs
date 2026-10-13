using System;
using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AccountingInventory.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TenantDbContext))]
[Migration("20261011185000_AddTableRegistry")]
partial class AddTableRegistry
{
    /// <inheritdoc />
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.0")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("AccountingInventory.Domain.Entities.TableRegistry", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uuid")
                .HasColumnName("id");

            b.Property<string>("SchemaName")
                .IsRequired()
                .ValueGeneratedOnAdd()
                .HasMaxLength(100)
                .HasColumnType("character varying(100)")
                .HasDefaultValue("tenant")
                .HasColumnName("schema_name");

            b.Property<string>("TableName")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("character varying(100)")
                .HasColumnName("table_name");

            b.Property<string>("DisplayName")
                .IsRequired()
                .HasMaxLength(150)
                .HasColumnType("character varying(150)")
                .HasColumnName("display_name");

            b.Property<string>("TableType")
                .IsRequired()
                .ValueGeneratedOnAdd()
                .HasMaxLength(50)
                .HasColumnType("character varying(50)")
                .HasDefaultValue("Data")
                .HasColumnName("table_type");

            b.Property<string>("Category")
                .ValueGeneratedOnAdd()
                .HasMaxLength(100)
                .HasColumnType("character varying(100)")
                .HasDefaultValue("General")
                .HasColumnName("category");

            b.Property<string>("Description")
                .HasColumnType("text")
                .HasColumnName("description");

            b.Property<bool>("IsSystem")
                .ValueGeneratedOnAdd()
                .HasColumnType("boolean")
                .HasDefaultValue(false)
                .HasColumnName("is_system");

            b.Property<DateTime>("CreatedAt")
                .ValueGeneratedOnAdd()
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_at");

            b.Property<DateTime?>("ModifiedAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("modified_at");

            b.HasKey("Id")
                .HasName("pk_table_registry");

            b.HasIndex("SchemaName", "TableName")
                .IsUnique()
                .HasDatabaseName("ix_table_registry_schema_table");

            b.ToTable("table_registry", "tenant");
        });
#pragma warning restore 612, 618
    }
}

