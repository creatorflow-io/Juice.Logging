﻿// <auto-generated />
using System;
using Juice.Extensions.Logging.EF.LogMetrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Juice.Extensions.Logging.EF.SqlServer.MetricsMigrations
{
    [DbContext(typeof(LogMetricsDbContext))]
    partial class LogMetricsDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.11")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("Juice.Extensions.Logging.EF.LogMetrics.CategoryLogMetric", b =>
                {
                    b.Property<string>("Category")
                        .HasColumnType("nvarchar(450)");

                    b.Property<DateTimeOffset>("Timestamp")
                        .HasColumnType("datetimeoffset");

                    b.Property<long>("Criticals")
                        .HasColumnType("bigint");

                    b.Property<long>("Errors")
                        .HasColumnType("bigint");

                    b.Property<long>("Warnings")
                        .HasColumnType("bigint");

                    b.HasKey("Category", "Timestamp");

                    b.ToTable("CategoryLogMetrics", "App");
                });

            modelBuilder.Entity("Juice.Extensions.Logging.EF.LogMetrics.OperationLogMetric", b =>
                {
                    b.Property<string>("Operation")
                        .HasColumnType("nvarchar(450)");

                    b.Property<DateTimeOffset>("Timestamp")
                        .HasColumnType("datetimeoffset");

                    b.Property<long>("Criticals")
                        .HasColumnType("bigint");

                    b.Property<long>("Errors")
                        .HasColumnType("bigint");

                    b.Property<long>("Warnings")
                        .HasColumnType("bigint");

                    b.HasKey("Operation", "Timestamp");

                    b.ToTable("OperationLogMetrics", "App");
                });

            modelBuilder.Entity("Juice.Extensions.Logging.EF.LogMetrics.ServiceLogMetric", b =>
                {
                    b.Property<Guid>("ServiceId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("Timestamp")
                        .HasColumnType("datetimeoffset");

                    b.Property<long>("Criticals")
                        .HasColumnType("bigint");

                    b.Property<long>("Errors")
                        .HasColumnType("bigint");

                    b.Property<long>("Warnings")
                        .HasColumnType("bigint");

                    b.HasKey("ServiceId", "Timestamp");

                    b.ToTable("ServiceLogMetrics", "App");
                });
#pragma warning restore 612, 618
        }
    }
}
