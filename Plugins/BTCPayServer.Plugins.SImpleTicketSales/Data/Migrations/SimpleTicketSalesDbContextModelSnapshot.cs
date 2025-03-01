﻿// <auto-generated />
using System;
using BTCPayServer.Plugins.SimpleTicketSales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BTCPayServer.Plugins.SimpleTicketSales.Data.Migrations
{
    [DbContext(typeof(SimpleTicketSalesDbContext))]
    partial class SimpleTicketSalesDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("BTCPayServer.Plugins.Ghost")
                .HasAnnotation("ProductVersion", "8.0.7")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("BTCPayServer.Plugins.SimpleTicketSales.Data.TicketSalesEvent", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("text");

                    b.Property<decimal>("Amount")
                        .HasColumnType("numeric");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Currency")
                        .HasColumnType("text");

                    b.Property<string>("Description")
                        .HasColumnType("text");

                    b.Property<string>("EmailBody")
                        .HasColumnType("text");

                    b.Property<string>("EmailSubject")
                        .HasColumnType("text");

                    b.Property<DateTime>("EventDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("EventImageUrl")
                        .HasColumnType("text");

                    b.Property<string>("EventLink")
                        .HasColumnType("text");

                    b.Property<bool>("HasMaximumCapacity")
                        .HasColumnType("boolean");

                    b.Property<int?>("MaximumEventCapacity")
                        .HasColumnType("integer");

                    b.Property<string>("StoreId")
                        .HasColumnType("text");

                    b.Property<string>("Title")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("TicketSalesEvents", "BTCPayServer.Plugins.Ghost");
                });

            modelBuilder.Entity("BTCPayServer.Plugins.SimpleTicketSales.Data.TicketSalesEventTicket", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("text");

                    b.Property<decimal>("Amount")
                        .HasColumnType("numeric");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Currency")
                        .HasColumnType("text");

                    b.Property<string>("Email")
                        .HasColumnType("text");

                    b.Property<bool>("EmailSent")
                        .HasColumnType("boolean");

                    b.Property<string>("EventId")
                        .HasColumnType("text");

                    b.Property<string>("InvoiceId")
                        .HasColumnType("text");

                    b.Property<string>("InvoiceStatus")
                        .HasColumnType("text");

                    b.Property<string>("Name")
                        .HasColumnType("text");

                    b.Property<string>("PaymentStatus")
                        .HasColumnType("text");

                    b.Property<DateTime>("PurchaseDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("StoreId")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("TicketSalesEventTickets", "BTCPayServer.Plugins.Ghost");
                });

            modelBuilder.Entity("BTCPayServer.Plugins.SimpleTicketSales.Data.TicketSalesTransaction", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("text");

                    b.Property<decimal>("Amount")
                        .HasColumnType("numeric");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Currency")
                        .HasColumnType("text");

                    b.Property<string>("InvoiceId")
                        .HasColumnType("text");

                    b.Property<string>("InvoiceStatus")
                        .HasColumnType("text");

                    b.Property<string>("MemberId")
                        .HasColumnType("text");

                    b.Property<string>("PaymentRequestId")
                        .HasColumnType("text");

                    b.Property<DateTime>("PeriodEnd")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime>("PeriodStart")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("StoreId")
                        .HasColumnType("text");

                    b.Property<string>("TierId")
                        .HasColumnType("text");

                    b.Property<int>("TransactionStatus")
                        .HasColumnType("integer");

                    b.Property<string>("TxnId")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("TicketSalesTransactions", "BTCPayServer.Plugins.Ghost");
                });
#pragma warning restore 612, 618
        }
    }
}
