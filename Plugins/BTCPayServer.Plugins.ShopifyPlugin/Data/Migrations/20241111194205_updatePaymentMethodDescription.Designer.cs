﻿// <auto-generated />
using System;
using BTCPayServer.Plugins.ShopifyPlugin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BTCPayServer.Plugins.ShopifyPlugin.Data.Migrations
{
    [DbContext(typeof(ShopifyDbContext))]
    [Migration("20241111194205_updatePaymentMethodDescription")]
    partial class updatePaymentMethodDescription
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("BTCPayServer.Plugins.Shopify")
                .HasAnnotation("ProductVersion", "8.0.7")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("BTCPayServer.Plugins.ShopifyPlugin.Data.Order", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("text");

                    b.Property<string>("CheckoutId")
                        .HasColumnType("text");

                    b.Property<string>("CheckoutToken")
                        .HasColumnType("text");

                    b.Property<string>("FinancialStatus")
                        .HasColumnType("text");

                    b.Property<string>("OrderId")
                        .HasColumnType("text");

                    b.Property<string>("OrderNumber")
                        .HasColumnType("text");

                    b.Property<string>("ShopName")
                        .HasColumnType("text");

                    b.Property<string>("StoreId")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("Orders", "BTCPayServer.Plugins.Shopify");
                });

            modelBuilder.Entity("BTCPayServer.Plugins.ShopifyPlugin.Data.ShopifySetting", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("text");

                    b.Property<string>("ApiKey")
                        .HasColumnType("text");

                    b.Property<string>("ApiSecret")
                        .HasColumnType("text");

                    b.Property<string>("ApplicationUserId")
                        .HasColumnType("text");

                    b.Property<DateTimeOffset?>("IntegratedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Password")
                        .HasColumnType("text");

                    b.Property<string>("PaymentText")
                        .HasColumnType("text");

                    b.Property<string>("ShopName")
                        .HasColumnType("text");

                    b.Property<string>("StoreId")
                        .HasColumnType("text");

                    b.Property<string>("StoreName")
                        .HasColumnType("text");

                    b.Property<string>("WebhookId")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("ShopifySettings", "BTCPayServer.Plugins.Shopify");
                });
#pragma warning restore 612, 618
        }
    }
}
