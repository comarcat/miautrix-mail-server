        ConfigureTenantScoped<CalendarEvent>(modelBuilder, "calendar_events");
        modelBuilder.Entity<CalendarEvent>(entity =>
        {
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Title).HasColumnName("title").IsRequired();
            entity.Property(e => e.StartTime).HasColumnName("start_time");
            entity.Property(e => e.EndTime).HasColumnName("end_time");
            entity.Property(e => e.Location).HasColumnName("location");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Organizer).HasColumnName("organizer");
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.Visibility).HasColumnName("visibility").IsRequired();
            entity.Property(e => e.ShowAs).HasColumnName("show_as").IsRequired();
            entity.Property(e => e.RecurrenceFrequency).HasColumnName("recurrence_frequency");
            entity.Property(e => e.RecurrenceInterval).HasColumnName("recurrence_interval").HasDefaultValue(1);
            entity.Property(e => e.RecurrenceUntil).HasColumnName("recurrence_until");
            entity.Property(e => e.Sequence).HasColumnName("sequence").HasDefaultValue(0).IsRequired();

            entity.HasIndex(e => new { e.TenantId, e.UserId, e.StartTime })
                .HasDatabaseName("idx_calendar_events_tenant_id_user_id_start_time");
        });
