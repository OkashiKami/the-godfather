﻿using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using TheGodfather.Common.Collections;
using TheGodfather.Database;
using TheGodfather.Exceptions;
using TheGodfather.Modules.Administration.Common;
using TheGodfather.Services;

namespace TheGodfather.Modules.Administration.Services
{
    public sealed class AntifloodService : ProtectionServiceBase
    {
        private readonly ConcurrentDictionary<ulong, ConcurrentHashSet<DiscordMember>> guildFloodUsers;


        public AntifloodService(DbContextBuilder dbb, LoggingService ls, SchedulingService ss, GuildConfigService gcs)
            : base(dbb, ls, ss, gcs, "_gf: Flooding")
        {
            this.guildFloodUsers = new ConcurrentDictionary<ulong, ConcurrentHashSet<DiscordMember>>();
        }


        public override bool TryAddGuildToWatch(ulong gid)
            => this.guildFloodUsers.TryAdd(gid, new ConcurrentHashSet<DiscordMember>());

        public override bool TryRemoveGuildFromWatch(ulong gid)
            => this.guildFloodUsers.TryRemove(gid, out _);


        public async Task HandleMemberJoinAsync(GuildMemberAddEventArgs e, AntifloodSettings settings)
        {
            if (!this.guildFloodUsers.ContainsKey(e.Guild.Id) && !this.TryAddGuildToWatch(e.Guild.Id))
                throw new ConcurrentOperationException("Failed to add guild to antiflood watch list!");

            if (!this.guildFloodUsers[e.Guild.Id].Add(e.Member))
                throw new ConcurrentOperationException("Failed to add member to antiflood watch list!");

            if (this.guildFloodUsers[e.Guild.Id].Count >= settings.Sensitivity) {
                foreach (DiscordMember m in this.guildFloodUsers[e.Guild.Id]) {
                    await this.PunishMemberAsync(e.Guild, m, settings.Action);
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }
                this.guildFloodUsers[e.Guild.Id].Clear();
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(settings.Cooldown));

            if (this.guildFloodUsers.ContainsKey(e.Guild.Id) && !this.guildFloodUsers[e.Guild.Id].TryRemove(e.Member))
                throw new ConcurrentOperationException("Failed to remove member from antiflood watch list!");
        }
    }
}
