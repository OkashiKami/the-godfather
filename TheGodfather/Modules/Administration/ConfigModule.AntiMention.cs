﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using TheGodfather.Attributes;
using TheGodfather.Database.Models;
using TheGodfather.Exceptions;
using TheGodfather.Extensions;
using TheGodfather.Modules.Administration.Common;
using TheGodfather.Modules.Administration.Extensions;
using TheGodfather.Modules.Administration.Services;

namespace TheGodfather.Modules.Administration
{
    public sealed partial class ConfigModule
    {
        [Group("antimention")]
        [Aliases("am")]
        public sealed class AntiMentionModule : TheGodfatherServiceModule<AntiMentionService>
        {
            #region config antimention
            [GroupCommand, Priority(3)]
            public async Task ExecuteGroupAsync(CommandContext ctx,
                                               [Description("desc-enable")] bool enable,
                                               [Description("desc-sens")] short sens,
                                               [Description("desc-punish-action")] Punishment.Action action = Punishment.Action.TemporaryMute)
            {
                if (sens is < AntiMentionSettings.MinSensitivity or > AntiMentionSettings.MaxSensitivity)
                    throw new CommandFailedException(ctx, "cmd-err-range-sens", AntiMentionSettings.MinSensitivity, AntiMentionSettings.MaxSensitivity);

                var settings = new AntiMentionSettings {
                    Action = action,
                    Enabled = enable,
                    Sensitivity = sens
                };

                await ctx.Services.GetRequiredService<GuildConfigService>().ModifyConfigAsync(ctx.Guild.Id, gcfg => gcfg.AntiMentionSettings = settings);

                await ctx.GuildLogAsync(emb => {
                    emb.WithLocalizedTitle("evt-cfg-upd");
                    emb.WithColor(this.ModuleColor);
                    if (enable) {
                        emb.WithLocalizedDescription("evt-am-enable");
                        emb.AddLocalizedTitleField("str-sensitivity", settings.Sensitivity, inline: true);
                        emb.AddLocalizedTitleField("str-punish-action", settings.Action.Humanize(), inline: true);
                    } else {
                        emb.WithLocalizedDescription("evt-am-disable");
                    }
                });

                await ctx.InfoAsync(enable ? "evt-am-enable" : "evt-am-disable");
            }

            [GroupCommand, Priority(2)]
            public Task ExecuteGroupAsync(CommandContext ctx,
                                         [Description("desc-enable")] bool enable,
                                         [Description("desc-punish-action")] Punishment.Action action,
                                         [Description("desc-sens")] short sens = 5)
                => this.ExecuteGroupAsync(ctx, enable, sens, action);

            [GroupCommand, Priority(1)]
            public Task ExecuteGroupAsync(CommandContext ctx,
                                         [Description("desc-enable")] bool enable)
                => this.ExecuteGroupAsync(ctx, enable, 5, Punishment.Action.TemporaryMute);

            [GroupCommand, Priority(0)]
            public async Task ExecuteGroupAsync(CommandContext ctx)
            {
                IReadOnlyList<ExemptedMentionEntity> exempts = await this.Service.GetExemptsAsync(ctx.Guild.Id);
                string? exemptString = await exempts.FormatExemptionsAsync(ctx.Client);
                await ctx.WithGuildConfigAsync(gcfg => {
                    return ctx.RespondWithLocalizedEmbedAsync(emb => {
                        emb.WithLocalizedTitle("str-antimention");
                        emb.WithDescription(gcfg.AntiMentionSettings.ToEmbedFieldString(ctx.Guild.Id, this.Localization));
                        emb.WithColor(this.ModuleColor);
                        if (exemptString is { })
                            emb.AddLocalizedTitleField("str-exempts", exemptString, inline: true);
                    });
                });
            }
            #endregion

            #region config antimention action
            [Command("action")]
            [Aliases("setaction", "setact", "act", "a")]
            public async Task SetActionAsync(CommandContext ctx,
                                            [Description("desc-punish-action")] Punishment.Action? action = null)
            {
                if (action is null) {
                    await ctx.WithGuildConfigAsync(gcfg => ctx.InfoAsync(this.ModuleColor, "evt-am-action", gcfg.AntispamAction.Humanize()));
                    return;
                }

                await ctx.Services.GetRequiredService<GuildConfigService>().ModifyConfigAsync(ctx.Guild.Id, cfg => {
                    cfg.AntispamAction = action.Value;
                });

                await ctx.GuildLogAsync(emb => {
                    emb.WithLocalizedTitle("evt-cfg-upd");
                    emb.WithColor(this.ModuleColor);
                    emb.WithLocalizedDescription("evt-am-action", action.Value.Humanize());
                });

                await ctx.InfoAsync(this.ModuleColor, "evt-am-action", action.Value.Humanize());
            }
            #endregion

            #region config antimention sensitivity
            [Command("sensitivity")]
            [Aliases("setsensitivity", "setsens", "sens", "s")]
            public async Task SetSensitivityAsync(CommandContext ctx,
                                                 [Description("desc-sens")] short? sens = null)
            {
                if (sens is null) {
                    await ctx.WithGuildConfigAsync(gcfg => ctx.InfoAsync(this.ModuleColor, "evt-am-sens", gcfg.AntispamSensitivity));
                    return;
                }

                if (sens is < AntiMentionSettings.MinSensitivity or > AntiMentionSettings.MaxSensitivity)
                    throw new CommandFailedException(ctx, "cmd-err-range-sens", AntiMentionSettings.MinSensitivity, AntiMentionSettings.MaxSensitivity);

                await ctx.Services.GetRequiredService<GuildConfigService>().ModifyConfigAsync(ctx.Guild.Id, cfg => {
                    cfg.AntispamSensitivity = sens.Value;
                });

                await ctx.GuildLogAsync(emb => {
                    emb.WithLocalizedTitle("evt-cfg-upd");
                    emb.WithColor(this.ModuleColor);
                    emb.WithLocalizedDescription("evt-am-sens", sens.Value);
                });

                await ctx.InfoAsync(this.ModuleColor, "evt-am-sens", sens.Value);
            }
            #endregion

            #region config antimention reset
            [Command("reset"), UsesInteractivity]
            [Aliases("default", "def", "s", "rr")]
            public async Task ResetAsync(CommandContext ctx)
            {
                await ctx.WithGuildConfigAsync(gcfg => {
                    return !gcfg.AntispamEnabled ? throw new CommandFailedException(ctx, "cmd-err-reset-am-off") : Task.CompletedTask;
                });

                if (!await ctx.WaitForBoolReplyAsync("q-setup-reset"))
                    return;

                var settings = new AntiMentionSettings();
                await this.ExecuteGroupAsync(ctx, true, settings.Action, settings.Sensitivity);
            }
            #endregion

            #region config antimention exempt
            [Command("exempt"), Priority(2)]
            [Aliases("ex", "exc")]
            public async Task ExemptAsync(CommandContext ctx,
                                         [Description("desc-exempt-user")] params DiscordMember[] members)
            {
                if (members is null || !members.Any())
                    throw new CommandFailedException(ctx, "cmd-err-exempt");

                await this.Service.ExemptAsync(ctx.Guild.Id, ExemptedEntityType.Member, members.SelectIds());
                await ctx.InfoAsync(this.ModuleColor);
            }

            [Command("exempt"), Priority(1)]
            public async Task ExemptAsync(CommandContext ctx,
                                         [Description("desc-exempt-role")] params DiscordRole[] roles)
            {
                if (roles is null || !roles.Any())
                    throw new CommandFailedException(ctx, "cmd-err-exempt");

                await this.Service.ExemptAsync(ctx.Guild.Id, ExemptedEntityType.Role, roles.SelectIds());
                await ctx.InfoAsync(this.ModuleColor);
            }

            [Command("exempt"), Priority(0)]
            public async Task ExemptAsync(CommandContext ctx,
                                         [Description("desc-exempt-chn")] params DiscordChannel[] channels)
            {
                if (channels is null || !channels.Any())
                    throw new CommandFailedException(ctx, "cmd-err-exempt");

                await this.Service.ExemptAsync(ctx.Guild.Id, ExemptedEntityType.Channel, channels.SelectIds());
                await ctx.InfoAsync(this.ModuleColor);
            }
            #endregion

            #region config antimention unexempt
            [Command("unexempt"), Priority(2)]
            [Aliases("unex", "uex")]
            public async Task UnxemptAsync(CommandContext ctx,
                                          [Description("desc-unexempt-user")] params DiscordMember[] members)
            {
                if (members is null || !members.Any())
                    throw new CommandFailedException(ctx, "cmd-err-exempt");

                await this.Service.UnexemptAsync(ctx.Guild.Id, ExemptedEntityType.Member, members.SelectIds());
                await ctx.InfoAsync(this.ModuleColor);
            }

            [Command("unexempt"), Priority(1)]
            public async Task UnxemptAsync(CommandContext ctx,
                                          [Description("desc-unexempt-role")] params DiscordRole[] roles)
            {
                if (roles is null || !roles.Any())
                    throw new CommandFailedException(ctx, "cmd-err-exempt");

                await this.Service.UnexemptAsync(ctx.Guild.Id, ExemptedEntityType.Role, roles.SelectIds());
                await ctx.InfoAsync(this.ModuleColor);
            }

            [Command("unexempt"), Priority(0)]
            public async Task UnxemptAsync(CommandContext ctx,
                                          [Description("desc-unexempt-chn")] params DiscordChannel[] channels)
            {
                if (channels is null || !channels.Any())
                    throw new CommandFailedException(ctx, "cmd-err-exempt");

                await this.Service.UnexemptAsync(ctx.Guild.Id, ExemptedEntityType.Channel, channels.SelectIds());
                await ctx.InfoAsync(this.ModuleColor);
            }
            #endregion
        }
    }
}
