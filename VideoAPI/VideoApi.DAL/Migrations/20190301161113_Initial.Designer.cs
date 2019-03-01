﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VideoApi.DAL;

namespace VideoApi.DAL.Migrations
{
    [DbContext(typeof(VideoApiDbContext))]
    [Migration("20190301161113_Initial")]
    partial class Initial
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "2.2.2-servicing-10034")
                .HasAnnotation("Relational:MaxIdentifierLength", 128)
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("VideoApi.Domain.Conference", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("CaseNumber");

                    b.Property<string>("CaseType");

                    b.Property<Guid>("HearingRefId");

                    b.Property<DateTime>("ScheduledDateTime");

                    b.HasKey("Id");

                    b.ToTable("Conference");
                });

            modelBuilder.Entity("VideoApi.Domain.ConferenceStatus", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>("ConferenceState");

                    b.Property<DateTime>("TimeStamp");

                    b.HasKey("Id");

                    b.ToTable("ConferenceStatus");
                });

            modelBuilder.Entity("VideoApi.Domain.Event", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>("EventType");

                    b.Property<string>("ExternalEventId");

                    b.Property<DateTime>("ExternalTimestamp");

                    b.Property<string>("ParticipantId");

                    b.Property<string>("Reason");

                    b.Property<DateTime>("Timestamp");

                    b.Property<int?>("TransferredFrom");

                    b.Property<int?>("TransferredTo");

                    b.HasKey("Id");

                    b.ToTable("Event");
                });

            modelBuilder.Entity("VideoApi.Domain.Participant", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>("CaseTypeGroup");

                    b.Property<string>("DisplayName");

                    b.Property<string>("HearingRole");

                    b.Property<string>("Name");

                    b.Property<Guid>("ParticipantRefId");

                    b.Property<string>("Username");

                    b.HasKey("Id");

                    b.ToTable("Participant");
                });

            modelBuilder.Entity("VideoApi.Domain.ParticipantStatus", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>("ParticipantState");

                    b.Property<DateTime>("TimeStamp");

                    b.HasKey("Id");

                    b.ToTable("ParticipantStatus");
                });
#pragma warning restore 612, 618
        }
    }
}
