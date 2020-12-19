﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using TheGodfather.Database;
using TheGodfather.Database.Models;
using TheGodfather.Modules.Administration.Common;
using TheGodfather.Services;

namespace TheGodfather.Modules.Administration.Services
{
    public abstract class ProtectionService : ITheGodfatherService, IDisposable
    {
        protected SemaphoreSlim csem = new SemaphoreSlim(1, 1);
        protected string reason;
        protected readonly TheGodfatherShard shard;

        public bool IsDisabled => false;


        protected ProtectionService(TheGodfatherShard shard, string reason)
        {
            this.shard = shard;
            this.reason = reason;
        }


        public async Task PunishMemberAsync(DiscordGuild guild, DiscordMember member, PunishmentAction type, TimeSpan? cooldown = null, string? reason = null)
        {
            try {
                DiscordRole muteRole;
                GuildTask gt;
                switch (type) {
                    case PunishmentAction.Kick:
                        await member.RemoveAsync(reason ?? this.reason);
                        break;
                    case PunishmentAction.PermanentMute:
                        muteRole = await this.GetOrCreateMuteRoleAsync(guild);
                        if (member.Roles.Contains(muteRole))
                            return;
                        await member.GrantRoleAsync(muteRole, reason ?? this.reason);
                        break;
                    case PunishmentAction.PermanentBan:
                        await member.BanAsync(1, reason: reason ?? this.reason);
                        break;
                    case PunishmentAction.TemporaryBan:
                        await member.BanAsync(0, reason: reason ?? this.reason);
                        gt = new GuildTask {
                            ExecutionTime = DateTimeOffset.Now + (cooldown ?? TimeSpan.FromDays(1)),
                            GuildId = guild.Id,
                            UserId = member.Id,
                            Type = ScheduledTaskType.Unban,
                        };
                        await this.shard.Services.GetRequiredService<SchedulingService>().ScheduleAsync(gt);
                        break;
                    case PunishmentAction.TemporaryMute:
                        muteRole = await this.GetOrCreateMuteRoleAsync(guild);
                        if (member.Roles.Contains(muteRole))
                            return;
                        await member.GrantRoleAsync(muteRole, reason ?? this.reason);
                        gt = new GuildTask {
                            ExecutionTime = DateTimeOffset.Now + (cooldown ?? TimeSpan.FromHours(1)),
                            GuildId = guild.Id,
                            RoleId = muteRole.Id,
                            UserId = member.Id,
                            Type = ScheduledTaskType.Unmute,
                        };
                        await this.shard.Services.GetRequiredService<SchedulingService>().ScheduleAsync(gt);
                        break;
                }
            } catch {
                if (LoggingService.IsLogEnabledForGuild(this.shard, guild.Id, out LoggingService log, out LocalizedEmbedBuilder emb)) {
                    emb.WithLocalizedTitle("err-punish-failed");
                    emb.WithColor(DiscordColor.Red);
                    emb.AddLocalizedTitleField("str-user", member);
                    emb.AddLocalizedTitleField("str-rsn", reason ?? this.reason);
                    await log.LogAsync(guild, emb.Build());
                }
            }
        }

        public async Task<DiscordRole> GetOrCreateMuteRoleAsync(DiscordGuild guild)
        {
            DiscordRole? muteRole = null;

            await this.csem.WaitAsync();
            try {
                using TheGodfatherDbContext db = this.shard.Database.CreateContext();
                GuildConfig gcfg = await this.shard.Services.GetRequiredService<GuildConfigService>().GetConfigAsync(guild.Id);
                muteRole = guild.GetRole(gcfg.MuteRoleId);
                if (muteRole is null)
                    muteRole = guild.Roles.Select(kvp => kvp.Value).FirstOrDefault(r => r.Name.ToLowerInvariant() == "gf_mute");
                if (muteRole is null) {
                    muteRole = await guild.CreateRoleAsync("gf_mute", hoist: false, mentionable: false);

                    IEnumerable<DiscordChannel> overwriteTargets = guild.Channels
                        .Select(kvp => kvp.Value)
                        .Where(c => c.Type == ChannelType.Category
                                 || ((c.Type == ChannelType.Text || c.Type == ChannelType.Voice) && c.Parent is null)
                        );

                    await Task.WhenAll(overwriteTargets.Select(c => AddOverwrite(c, muteRole)));

                    gcfg.MuteRoleId = muteRole.Id;
                    db.Configs.Update(gcfg);
                    await db.SaveChangesAsync();
                }
            } finally {
                this.csem.Release();
            }

            return muteRole;


            static async Task AddOverwrite(DiscordChannel channel, DiscordRole muteRole)
            {
                await channel.AddOverwriteAsync(
                    muteRole,
                    deny: Permissions.SendMessages
                        | Permissions.SendTtsMessages
                        | Permissions.AddReactions
                        | Permissions.Speak
                );
                await Task.Delay(10);
            }
        }


        public abstract bool TryAddGuildToWatch(ulong gid);
        public abstract bool TryRemoveGuildFromWatch(ulong gid);
        public abstract void Dispose();
    }
}
