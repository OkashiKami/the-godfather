﻿#region USING_DIRECTIVES
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using TheGodfather.Attributes;
using TheGodfather.Exceptions;
using TheGodfather.Modules.Search.Services;
#endregion

namespace TheGodfather.Modules.Search
{
    [Group("steam"), Module(ModuleType.Searches), NotBlocked]
    [Description("Steam commands. Group call searches steam profiles for a given ID.")]
    [Aliases("s", "st")]

    [Cooldown(3, 5, CooldownBucketType.Channel)]
    public class SteamModule : TheGodfatherServiceModule<SteamService>
    {

        #region COMMAND_STEAM_PROFILE
        [Command("profile")]
        [Description("Get Steam user information for user based on his ID.")]
        [Aliases("id", "user")]

        public async Task InfoAsync(CommandContext ctx,
                                   [Description("ID.")] ulong id)
        {
            if (this.Service.IsDisabled)
                throw new ServiceDisabledException(ctx);

            DiscordEmbed em = await this.Service.GetEmbeddedInfoAsync(id);
            if (em is null) {
                await this.InformFailureAsync(ctx, "User with such ID does not exist!");
                return;
            }

            await ctx.RespondAsync(embed: em);
        }
        #endregion
    }
}